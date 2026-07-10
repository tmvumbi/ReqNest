using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Notifications;
using ReqNest.Core.Reports;
using ReqNest.Core.Storage;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;

namespace ReqNest.Api.Endpoints;

public static class ReportEndpoints
{
    private const int ReportRowLimit = 20_000;
    private static readonly string[] ReportTypes =
    [
        "inventory", "created-resolved", "aging", "resolution", "throughput", "workload", "sla", "workflow", "project-comparison", "activity",
    ];

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reports")
            .RequireAuthorization()
            .WithTags("Reports");
        group.MapGet("/{reportType}", GetAsync);
        group.MapGet("/{reportType}/csv", ExportCsvAsync);
        group.MapPost("/exports", ExportAsync);
        group.MapGet("/exports", ListExportsAsync);
        group.MapGet("/exports/{exportId:guid}/download", DownloadExportAsync);
        group.MapGet("/schedules", ListSchedulesAsync);
        group.MapPost("/schedules", CreateScheduleAsync);
        group.MapPut("/schedules/{scheduleId:guid}", UpdateScheduleAsync);
        group.MapDelete("/schedules/{scheduleId:guid}", DeleteScheduleAsync);
        group.MapPost("/schedules/{scheduleId:guid}/run", RunScheduleAsync);
        endpoints.MapGet("/api/dashboard", DashboardAsync)
            .RequireAuthorization()
            .WithTags("Dashboard");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        string reportType,
        Guid? projectId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        TicketPriority? priority,
        TicketType? type,
        Guid? assigneeUserId,
        bool? includeArchived,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var request = new ReportFilterRequest(projectId, from, to, priority, type, assigneeUserId, includeArchived ?? false);
        var result = await BuildReportAsync(reportType, request, httpContext, dbContext, cancellationToken);
        return result.Error ?? TypedResults.Ok(result.Report!);
    }

    private static async Task<IResult> ExportCsvAsync(
        string reportType,
        Guid? projectId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        TicketPriority? priority,
        TicketType? type,
        Guid? assigneeUserId,
        bool? includeArchived,
        AppLanguage? language,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (projectId is not null && !authorization.CanExportReports(projectId.Value))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var filter = new ReportFilterRequest(projectId, from, to, priority, type, assigneeUserId, includeArchived ?? false);
        var result = await BuildReportAsync(reportType, filter, httpContext, dbContext, cancellationToken);
        if (result.Error is not null)
        {
            return result.Error;
        }

        var french = language == AppLanguage.French;
        var csv = BuildCsv(result.Report!, french);
        return Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv; charset=utf-8",
            $"reqnest-{reportType}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static async Task<IResult> ListSchedulesAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var userId = httpContext.User.UserId();
        var schedules = await dbContext.ReportSchedules.AsNoTracking()
            .Where(entity => entity.OwnerUserId == userId)
            .OrderBy(entity => entity.Name)
            .Select(entity => new ReportScheduleResponse(
                entity.Id,
                entity.ProjectId,
                entity.Name,
                entity.ReportType,
                entity.FilterSnapshotJson,
                entity.Language,
                entity.Format,
                entity.Frequency,
                entity.IsActive,
                entity.NextRunAt,
                entity.LastRunAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<ReportScheduleResponse>>(schedules);
    }

    private static async Task<IResult> CreateScheduleAsync(
        UpsertReportScheduleRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = ValidateSchedule(request, authorization, httpContext);
        if (error is not null)
        {
            return error;
        }

        var schedule = new ReportSchedule
        {
            TenantId = authorization!.TenantId,
            OwnerUserId = httpContext.User.UserId(),
        };
        Apply(schedule, request);
        dbContext.ReportSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/reports/schedules/{schedule.Id}", ToResponse(schedule));
    }

    private static async Task<IResult> UpdateScheduleAsync(
        Guid scheduleId,
        UpsertReportScheduleRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = ValidateSchedule(request, authorization, httpContext);
        if (error is not null)
        {
            return error;
        }

        var userId = httpContext.User.UserId();
        var schedule = await dbContext.ReportSchedules.SingleOrDefaultAsync(
            entity => entity.Id == scheduleId && entity.OwnerUserId == userId,
            cancellationToken);
        if (schedule is null)
        {
            return ApiProblems.NotFound(httpContext, "Report schedule");
        }

        Apply(schedule, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(schedule));
    }

    private static async Task<IResult> DeleteScheduleAsync(
        Guid scheduleId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var userId = httpContext.User.UserId();
        var schedule = await dbContext.ReportSchedules.SingleOrDefaultAsync(
            entity => entity.Id == scheduleId && entity.OwnerUserId == userId,
            cancellationToken);
        if (schedule is null)
        {
            return ApiProblems.NotFound(httpContext, "Report schedule");
        }

        dbContext.ReportSchedules.Remove(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    internal static async Task<IResult> RunScheduleAsync(
        Guid scheduleId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IReportPdfGenerator pdfGenerator,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var userId = httpContext.User.UserId();
        var schedule = await dbContext.ReportSchedules.SingleOrDefaultAsync(
            entity => entity.Id == scheduleId && entity.OwnerUserId == userId,
            cancellationToken);
        if (schedule is null)
        {
            return ApiProblems.NotFound(httpContext, "Report schedule");
        }

        var filter = JsonSerializer.Deserialize<ReportFilterRequest>(schedule.FilterSnapshotJson);
        if (filter is null)
        {
            return ApiProblems.Conflict(httpContext, "The report schedule filter snapshot is invalid.");
        }

        schedule.LastRunAt = DateTimeOffset.UtcNow;
        schedule.NextRunAt = NextRun(schedule.LastRunAt.Value, schedule.Frequency);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (schedule.Format == ReportExportFormat.Pdf)
        {
            return await ExportAsync(
                new CreateReportExportRequest(schedule.ReportType, filter, schedule.Language),
                httpContext,
                dbContext,
                pdfGenerator,
                blobStorage,
                storageOptions,
                notificationService,
                cancellationToken);
        }

        var result = await BuildReportAsync(schedule.ReportType, filter, httpContext, dbContext, cancellationToken);
        if (result.Error is not null)
        {
            return result.Error;
        }

        var csv = BuildCsv(result.Report!, schedule.Language == AppLanguage.French);
        var completedAt = schedule.LastRunAt ?? DateTimeOffset.UtcNow;
        await notificationService.AddAsync(new NotificationMessage(
            authorization.TenantId,
            [schedule.OwnerUserId],
            schedule.OwnerUserId,
            NotificationType.ReportReady,
            schedule.ProjectId,
            null,
            $"report-schedule-ready:{schedule.Id}:{completedAt:O}",
            $"Scheduled report '{schedule.Name}' is ready.",
            $"Le rapport planifié « {schedule.Name} » est prêt.",
            "/app/reports",
            schedule.Id.ToString(),
            NotifyActor: true), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv; charset=utf-8",
            $"reqnest-{schedule.ReportType}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static async Task<IResult> ExportAsync(
        CreateReportExportRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IReportPdfGenerator pdfGenerator,
        IBlobStorageService blobStorage,
        IOptions<BlobStorageOptions> storageOptions,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var projectIds = request.Filter.ProjectId is null
            ? authorization.ProjectRoles.Keys.Concat(authorization.ProjectPermissions.Keys).Distinct().ToArray()
            : [request.Filter.ProjectId.Value];
        var canExport = request.Filter.ProjectId is not null
            ? authorization.CanExportReports(request.Filter.ProjectId.Value)
            : authorization.IsTenantAdministrator() ||
              authorization.HasTenantRole(AppRole.ProjectManager) ||
              authorization.AllProjectPermissions.Contains(AppPermission.ReportExport, StringComparer.Ordinal) ||
              projectIds.Length > 0 && projectIds.All(authorization.CanExportReports);
        if (!canExport)
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var result = await BuildReportAsync(request.ReportType, request.Filter, httpContext, dbContext, cancellationToken);
        if (result.Error is not null)
        {
            return result.Error;
        }

        var report = result.Report!;
        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(cancellationToken);
        var user = await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entity => entity.Id == httpContext.User.UserId(), cancellationToken);
        var french = request.Language == AppLanguage.French;
        var generatedAt = DateTimeOffset.UtcNow;
        byte[]? logoBytes = null;
        if (tenant.LogoBlobName is not null)
        {
            await using var logoStream = await blobStorage.OpenReadAsync(
                storageOptions.Value.DefaultContainer,
                tenant.LogoBlobName,
                cancellationToken);
            using var logoBuffer = new MemoryStream();
            await logoStream.CopyToAsync(logoBuffer, cancellationToken);
            logoBytes = logoBuffer.ToArray();
        }

        var tableLines = new List<string>
        {
            string.Join(" | ", report.Columns.Select(column => LocalizeColumn(column, french))),
        };
        tableLines.AddRange(report.Rows.Take(500).Select(row => string.Join(
            " | ",
            report.Columns.Select(column => LocalizeReportValue(row.GetValueOrDefault(column), french)))));
        var pdf = pdfGenerator.Generate(new ReportPdfContent(
            tenant.Name,
            french ? report.TitleFrench : report.TitleEnglish,
            french ? $"Généré : {generatedAt:u}" : $"Generated: {generatedAt:u}",
            french ? $"Fuseau horaire : {tenant.TimeZone}" : $"Time zone: {tenant.TimeZone}",
            french ? $"Demandé par : {user.DisplayName}" : $"Requested by: {user.DisplayName}",
            french ? $"Filtres : {JsonSerializer.Serialize(request.Filter)}" : $"Filters: {JsonSerializer.Serialize(request.Filter)}",
            french ? "Définitions" : "Metric definitions",
            tenant.ReportFooterText ?? "ReqNest",
            french ? report.DefinitionsFrench : report.DefinitionsEnglish,
            tableLines,
            logoBytes));
        var export = new ReportExport
        {
            TenantId = authorization.TenantId,
            RequestedByUserId = user.Id,
            ReportType = request.ReportType,
            FilterSnapshotJson = JsonSerializer.Serialize(request.Filter),
            Language = request.Language,
            TimeZone = tenant.TimeZone,
            Status = ReportExportStatus.Pending,
            ExpiresAt = generatedAt.AddDays(7),
        };
        dbContext.ReportExports.Add(export);
        var options = storageOptions.Value;
        var blobName = $"{authorization.TenantId:N}/reports/{export.Id:N}.pdf";
        try
        {
            await using var stream = new MemoryStream(pdf, writable: false);
            await blobStorage.UploadAsync(options.DefaultContainer, blobName, stream, "application/pdf", cancellationToken);
            export.ContainerName = options.DefaultContainer;
            export.BlobName = blobName;
            export.Status = ReportExportStatus.Ready;
            var audit = new AuditEvent
            {
                TenantId = authorization.TenantId,
                ActorUserId = user.Id,
                Action = "report.export.ready",
                TargetType = nameof(ReportExport),
                TargetId = export.Id.ToString(),
                Summary = "A PDF report export was generated.",
                CorrelationId = httpContext.TraceIdentifier,
            };
            dbContext.AuditEvents.Add(audit);
            await notificationService.AddAsync(new NotificationMessage(
                authorization.TenantId,
                [user.Id],
                user.Id,
                NotificationType.ReportReady,
                request.Filter.ProjectId,
                null,
                audit.Id.ToString(),
                "Your PDF report is ready.",
                "Votre rapport PDF est prêt.",
                "/app/reports",
                export.Id.ToString(),
                NotifyActor: true), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await blobStorage.DeleteIfExistsAsync(options.DefaultContainer, blobName, cancellationToken);
            throw;
        }

        return TypedResults.Accepted($"/api/reports/exports/{export.Id}/download", ToResponse(export));
    }

    private static async Task<IResult> ListExportsAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var userId = httpContext.User.UserId();
        var exports = await dbContext.ReportExports.AsNoTracking()
            .Where(entity => entity.RequestedByUserId == userId)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(100)
            .Select(entity => ToResponse(entity))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<ReportExportResponse>>(exports);
    }

    private static async Task<IResult> DownloadExportAsync(
        Guid exportId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        IBlobStorageService blobStorage,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var export = await dbContext.ReportExports.SingleOrDefaultAsync(
            entity => entity.Id == exportId && entity.RequestedByUserId == httpContext.User.UserId(),
            cancellationToken);
        if (export is null)
        {
            return ApiProblems.NotFound(httpContext, "Report export");
        }

        if (export.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            export.Status = ReportExportStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApiProblems.NotFound(httpContext, "Report export");
        }

        if (export.Status != ReportExportStatus.Ready || export.ContainerName is null || export.BlobName is null)
        {
            return ApiProblems.Conflict(httpContext, "The report export is not ready.", "report_not_ready");
        }

        var stream = await blobStorage.OpenReadAsync(export.ContainerName, export.BlobName, cancellationToken);
        return Results.Stream(stream, "application/pdf", $"reqnest-{export.ReportType}.pdf", enableRangeProcessing: true);
    }

    private static async Task<IResult> DashboardAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var projectIds = authorization.ProjectRoles.Keys.Concat(authorization.ProjectPermissions.Keys).Distinct().ToArray();
        var all = authorization.AllProjectRoles.Count > 0 || authorization.AllProjectPermissions.Count > 0;
        var userId = httpContext.User.UserId();
        var now = DateTimeOffset.UtcNow;
        var query = dbContext.Tickets.AsNoTracking()
            .Where(entity => all || projectIds.Contains(entity.ProjectId))
            .Where(entity => !entity.IsArchived);
        var assignedOpen = await query.CountAsync(entity => entity.AssigneeUserId == userId && !entity.WorkflowStatus.IsTerminal, cancellationToken);
        var urgent = await query.CountAsync(entity => entity.Priority == TicketPriority.Urgent && !entity.WorkflowStatus.IsTerminal, cancellationToken);
        var overdue = await query.CountAsync(entity => entity.DueAt < now && entity.ResolvedAt == null, cancellationToken);
        var slaRisk = await query.CountAsync(entity => entity.SlaState == SlaState.AtRisk || entity.SlaState == SlaState.Breached, cancellationToken);
        var unread = await dbContext.Notifications.CountAsync(entity => entity.RecipientUserId == userId && entity.ReadAt == null, cancellationToken);
        var recent = await query.OrderByDescending(entity => entity.UpdatedAt).Take(8)
            .Select(entity => new DashboardTicketResponse(entity.Id, entity.Key, entity.Title, entity.Priority, entity.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new DashboardResponse(assignedOpen, urgent, overdue, slaRisk, unread, recent));
    }

    private static async Task<ReportBuildResult> BuildReportAsync(
        string reportType,
        ReportFilterRequest filter,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return new ReportBuildResult(null, ApiProblems.TenantRequired(httpContext));
        }

        reportType = reportType.Trim().ToLowerInvariant();
        if (!ReportTypes.Contains(reportType, StringComparer.Ordinal))
        {
            return new ReportBuildResult(null, ApiProblems.NotFound(httpContext, "Report"));
        }

        if (filter.ProjectId is not null && !authorization.CanAccessProject(filter.ProjectId.Value))
        {
            return new ReportBuildResult(null, ApiProblems.NotFound(httpContext, "Project"));
        }

        var projectIds = authorization.ProjectRoles.Keys.Concat(authorization.ProjectPermissions.Keys).Distinct().ToArray();
        var all = authorization.AllProjectRoles.Count > 0 || authorization.AllProjectPermissions.Count > 0;
        var query = dbContext.Tickets.AsNoTracking()
            .Where(entity => all || projectIds.Contains(entity.ProjectId));
        if (!filter.IncludeArchived)
        {
            query = query.Where(entity => !entity.IsArchived);
        }

        if (filter.ProjectId is not null)
        {
            query = query.Where(entity => entity.ProjectId == filter.ProjectId);
        }

        if (filter.From is not null)
        {
            query = query.Where(entity => entity.CreatedAt >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(entity => entity.CreatedAt < filter.To);
        }

        if (filter.Priority is not null)
        {
            query = query.Where(entity => entity.Priority == filter.Priority);
        }

        if (filter.Type is not null)
        {
            query = query.Where(entity => entity.Type == filter.Type);
        }

        if (filter.AssigneeUserId is not null)
        {
            query = query.Where(entity => entity.AssigneeUserId == filter.AssigneeUserId);
        }

        var source = await query.OrderByDescending(entity => entity.CreatedAt)
            .Take(ReportRowLimit + 1)
            .Select(entity => new ReportTicketRow(
                entity.Id,
                entity.ProjectId,
                entity.Project.Key,
                entity.Project.NameEnglish,
                entity.Project.NameFrench,
                entity.Key,
                entity.Type,
                entity.Priority,
                entity.WorkflowStatus.Key,
                entity.WorkflowStatus.Category,
                entity.AssigneeUser == null ? "Unassigned" : entity.AssigneeUser.DisplayName,
                entity.ReporterUser.DisplayName,
                entity.CreatedAt,
                entity.FirstRespondedAt,
                entity.ResolvedAt,
                entity.UpdatedAt,
                entity.SlaState))
            .ToArrayAsync(cancellationToken);
        var truncated = source.Length > ReportRowLimit;
        source = source.Take(ReportRowLimit).ToArray();
        var report = CreateReport(reportType, source, truncated, filter, dbContext, authorization, cancellationToken);
        return new ReportBuildResult(await report, null);
    }

    private static async Task<ReportResponse> CreateReport(
        string reportType,
        IReadOnlyCollection<ReportTicketRow> source,
        bool truncated,
        ReportFilterRequest filter,
        ReqNestDbContext dbContext,
        TenantAuthorization authorization,
        CancellationToken cancellationToken)
    {
        var definitionsEnglish = new List<string>();
        var definitionsFrench = new List<string>();
        string titleEnglish;
        string titleFrench;
        string[] columns;
        IReadOnlyCollection<Dictionary<string, object?>> rows;

        switch (reportType)
        {
            case "inventory":
                titleEnglish = "Ticket inventory";
                titleFrench = "Inventaire des tickets";
                columns = ["Project", "Status", "Category", "Type", "Priority", "Count"];
                rows = source.GroupBy(item => new { item.ProjectKey, item.StatusKey, item.StatusCategory, item.Type, item.Priority })
                    .Select(group => Row(("Project", group.Key.ProjectKey), ("Status", group.Key.StatusKey),
                        ("Category", group.Key.StatusCategory), ("Type", group.Key.Type), ("Priority", group.Key.Priority), ("Count", group.Count())))
                    .OrderBy(row => row["Project"]).ToArray();
                definitionsEnglish.Add("Count is the current number of tickets matching each group.");
                definitionsFrench.Add("Le nombre correspond aux tickets actuels de chaque groupe.");
                break;
            case "created-resolved":
                titleEnglish = "Created vs. resolved trend";
                titleFrench = "Tendance créés et résolus";
                columns = ["Date", "Created", "Resolved", "NetBacklog"];
                var dates = source.Select(item => item.CreatedAt.Date)
                    .Concat(source.Where(item => item.ResolvedAt != null).Select(item => item.ResolvedAt!.Value.Date))
                    .Distinct().Order().ToArray();
                rows = dates.Select(date =>
                {
                    var created = source.Count(item => item.CreatedAt.Date == date);
                    var resolved = source.Count(item => item.ResolvedAt?.Date == date);
                    return Row(("Date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("Created", created),
                        ("Resolved", resolved), ("NetBacklog", created - resolved));
                }).ToArray();
                definitionsEnglish.Add("Resolved is entry into a terminal status; reopened tickets retain historical resolution events but are currently open.");
                definitionsFrench.Add("Résolu signifie l'entrée dans un statut terminal; les tickets rouverts sont actuellement ouverts.");
                break;
            case "aging":
                titleEnglish = "Ticket aging";
                titleFrench = "Ancienneté des tickets";
                columns = ["AgeBand", "Project", "Status", "Priority", "Assignee", "Count"];
                var today = DateTimeOffset.UtcNow;
                rows = source.Where(item => item.ResolvedAt == null)
                    .GroupBy(item => new
                    {
                        Band = AgeBand(today - item.CreatedAt),
                        item.ProjectKey,
                        item.StatusKey,
                        item.Priority,
                        item.Assignee,
                    })
                    .Select(group => Row(("AgeBand", group.Key.Band), ("Project", group.Key.ProjectKey), ("Status", group.Key.StatusKey),
                        ("Priority", group.Key.Priority), ("Assignee", group.Key.Assignee), ("Count", group.Count()))).ToArray();
                definitionsEnglish.Add("Age is elapsed time since ticket creation for tickets that are currently unresolved.");
                definitionsFrench.Add("L'ancienneté est le temps écoulé depuis la création des tickets non résolus.");
                break;
            case "resolution":
                titleEnglish = "Resolution performance";
                titleFrench = "Performance de résolution";
                columns = ["Project", "Priority", "Tickets", "MedianFirstResponseHours", "P90ResolutionHours"];
                rows = source.GroupBy(item => new { item.ProjectKey, item.Priority }).Select(group =>
                {
                    var first = group.Where(item => item.FirstRespondedAt != null)
                        .Select(item => (item.FirstRespondedAt!.Value - item.CreatedAt).TotalHours).Order().ToArray();
                    var resolved = group.Where(item => item.ResolvedAt != null)
                        .Select(item => (item.ResolvedAt!.Value - item.CreatedAt).TotalHours).Order().ToArray();
                    return Row(("Project", group.Key.ProjectKey), ("Priority", group.Key.Priority), ("Tickets", group.Count()),
                        ("MedianFirstResponseHours", Percentile(first, 0.5)), ("P90ResolutionHours", Percentile(resolved, 0.9)));
                }).ToArray();
                definitionsEnglish.Add("First response is the first comment by someone other than the reporter. Resolution is entry into a terminal status.");
                definitionsFrench.Add("La première réponse est le premier commentaire d'une personne autre que le rapporteur. La résolution est l'entrée dans un statut terminal.");
                break;
            case "throughput":
                titleEnglish = "Throughput";
                titleFrench = "Débit de résolution";
                columns = ["Month", "Project", "Type", "Contributor", "Completed"];
                rows = source.Where(item => item.ResolvedAt != null)
                    .GroupBy(item => new { Month = item.ResolvedAt!.Value.ToString("yyyy-MM"), item.ProjectKey, item.Type, item.Assignee })
                    .Select(group => Row(("Month", group.Key.Month), ("Project", group.Key.ProjectKey), ("Type", group.Key.Type),
                        ("Contributor", group.Key.Assignee), ("Completed", group.Count()))).ToArray();
                definitionsEnglish.Add("Completed tickets entered a terminal status during the period.");
                definitionsFrench.Add("Les tickets terminés sont entrés dans un statut terminal pendant la période.");
                break;
            case "workload":
                titleEnglish = "Workload";
                titleFrench = "Charge de travail";
                columns = ["Assignee", "Open", "InProgress", "Urgent"];
                rows = source.Where(item => item.ResolvedAt == null).GroupBy(item => item.Assignee)
                    .Select(group => Row(("Assignee", group.Key), ("Open", group.Count()),
                        ("InProgress", group.Count(item => item.StatusCategory == Core.Workflows.WorkflowStatusCategory.InProgress)),
                        ("Urgent", group.Count(item => item.Priority == TicketPriority.Urgent)))).ToArray();
                definitionsEnglish.Add("Open excludes tickets currently in a terminal status.");
                definitionsFrench.Add("Ouvert exclut les tickets actuellement dans un statut terminal.");
                break;
            case "sla":
                titleEnglish = "SLA performance";
                titleFrench = "Performance SLA";
                columns = ["Project", "Priority", "State", "Count"];
                rows = source.GroupBy(item => new { item.ProjectKey, item.Priority, item.SlaState })
                    .Select(group => Row(("Project", group.Key.ProjectKey), ("Priority", group.Key.Priority),
                        ("State", group.Key.SlaState), ("Count", group.Count()))).ToArray();
                definitionsEnglish.Add("Phase 1 SLA targets use elapsed clock time; business calendars are introduced in Phase 2.");
                definitionsFrench.Add("Les objectifs SLA de la phase 1 utilisent le temps écoulé; les calendriers ouvrés arrivent en phase 2.");
                break;
            case "workflow":
                titleEnglish = "Workflow flow";
                titleFrench = "Flux de travail";
                columns = ["Project", "Status", "Category", "CurrentTickets"];
                rows = source.GroupBy(item => new { item.ProjectKey, item.StatusKey, item.StatusCategory })
                    .Select(group => Row(("Project", group.Key.ProjectKey), ("Status", group.Key.StatusKey),
                        ("Category", group.Key.StatusCategory), ("CurrentTickets", group.Count()))).ToArray();
                definitionsEnglish.Add("Current tickets shows the present distribution; transition history remains available in ticket activity.");
                definitionsFrench.Add("Les tickets actuels montrent la distribution présente; l'historique des transitions reste dans l'activité.");
                break;
            case "project-comparison":
                titleEnglish = "Project comparison";
                titleFrench = "Comparaison des projets";
                columns = ["Project", "Inventory", "Open", "Resolved", "MedianResolutionHours"];
                rows = source.GroupBy(item => item.ProjectKey).Select(group =>
                {
                    var durations = group.Where(item => item.ResolvedAt != null)
                        .Select(item => (item.ResolvedAt!.Value - item.CreatedAt).TotalHours).Order().ToArray();
                    return Row(("Project", group.Key), ("Inventory", group.Count()),
                        ("Open", group.Count(item => item.ResolvedAt == null)), ("Resolved", group.Count(item => item.ResolvedAt != null)),
                        ("MedianResolutionHours", Percentile(durations, 0.5)));
                }).ToArray();
                definitionsEnglish.Add("Metrics use the same authorized filter snapshot for every project.");
                definitionsFrench.Add("Les mesures utilisent le même instantané de filtres autorisés pour chaque projet.");
                break;
            default:
                titleEnglish = "Activity report";
                titleFrench = "Rapport d'activité";
                columns = ["Date", "Action", "Count"];
                var ticketIds = source.Select(item => item.Id).ToArray();
                var targetIds = ticketIds.Select(item => item.ToString()).ToArray();
                var activities = await dbContext.AuditEvents.AsNoTracking()
                    .Where(entity => entity.TargetType == nameof(Core.Tickets.Ticket) && targetIds.Contains(entity.TargetId))
                    .OrderByDescending(entity => entity.CreatedAt)
                    .Take(ReportRowLimit)
                    .Select(entity => new { entity.CreatedAt, entity.Action })
                    .ToArrayAsync(cancellationToken);
                rows = activities.GroupBy(item => new { Date = item.CreatedAt.Date, item.Action })
                    .Select(group => Row(("Date", group.Key.Date.ToString("yyyy-MM-dd")), ("Action", group.Key.Action), ("Count", group.Count())))
                    .ToArray();
                definitionsEnglish.Add("Activity counts audited business actions; sensitive values are never included.");
                definitionsFrench.Add("L'activité compte les actions métier auditées; les valeurs sensibles ne sont jamais incluses.");
                break;
        }

        return new ReportResponse(
            reportType,
            titleEnglish,
            titleFrench,
            columns,
            rows,
            definitionsEnglish,
            definitionsFrench,
            filter,
            truncated,
            DateTimeOffset.UtcNow);
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value);

    private static string AgeBand(TimeSpan age) => age.TotalDays switch
    {
        < 1 => "<1d",
        < 3 => "1-3d",
        < 7 => "3-7d",
        < 14 => "7-14d",
        < 30 => "14-30d",
        _ => "30d+",
    };

    private static double? Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return null;
        }

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return Math.Round(sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)], 2);
    }

    private static string LocalizeColumn(string column, bool french)
    {
        if (!french)
        {
            return column;
        }

        return column switch
        {
            "Project" => "Projet",
            "Status" => "Statut",
            "Category" => "Catégorie",
            "Type" => "Type",
            "Priority" => "Priorité",
            "Count" => "Nombre",
            "Date" => "Date",
            "Created" => "Créés",
            "Resolved" => "Résolus",
            "NetBacklog" => "Variation du stock",
            "AgeBand" => "Tranche d'ancienneté",
            "Assignee" => "Responsable",
            "Tickets" => "Tickets",
            "MedianFirstResponseHours" => "Médiane 1re réponse (h)",
            "P90ResolutionHours" => "P90 résolution (h)",
            "Month" => "Mois",
            "Contributor" => "Contributeur",
            "Completed" => "Terminés",
            "Open" => "Ouverts",
            "InProgress" => "En cours",
            "Urgent" => "Urgents",
            "State" => "État",
            "CurrentTickets" => "Tickets actuels",
            "Inventory" => "Inventaire",
            "MedianResolutionHours" => "Médiane résolution (h)",
            "Action" => "Action",
            _ => column,
        };
    }

    private static string LocalizeReportValue(object? value, bool french)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (!french)
        {
            return text;
        }

        return text switch
        {
            "ToDo" => "À faire",
            "InProgress" => "En cours",
            "Done" => "Terminé",
            "TODO" => "À FAIRE",
            "IN_PROGRESS" => "EN COURS",
            "DONE" => "TERMINÉ",
            "Low" => "Faible",
            "Normal" => "Normale",
            "High" => "Élevée",
            "Urgent" => "Urgente",
            "Incident" => "Incident",
            "ServiceRequest" => "Demande de service",
            "Task" => "Tâche",
            "Problem" => "Problème",
            "Unassigned" => "Non attribué",
            "None" => "Aucun",
            "OnTrack" => "Dans les délais",
            "AtRisk" => "À risque",
            "Breached" => "Dépassé",
            "Met" => "Respecté",
            _ => text,
        };
    }

    private static string BuildCsv(ReportResponse report, bool french)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", report.Columns.Select(column => CsvCell(LocalizeColumn(column, french)))));
        foreach (var row in report.Rows)
        {
            builder.AppendLine(string.Join(",", report.Columns.Select(column =>
                CsvCell(LocalizeReportValue(row.GetValueOrDefault(column), french)))));
        }

        return builder.ToString();
    }

    private static string CsvCell(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static IResult? ValidateSchedule(
        UpsertReportScheduleRequest request,
        TenantAuthorization? authorization,
        HttpContext context)
    {
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160 ||
            !ReportTypes.Contains(request.ReportType, StringComparer.Ordinal) ||
            request.Filter.ValueKind != JsonValueKind.Object)
        {
            return ApiProblems.Validation(context, "The report schedule is invalid.");
        }

        if (request.ProjectId is not null && !authorization.CanExportReports(request.ProjectId.Value))
        {
            return ApiProblems.Forbidden(context);
        }

        return null;
    }

    private static void Apply(ReportSchedule schedule, UpsertReportScheduleRequest request)
    {
        schedule.ProjectId = request.ProjectId;
        schedule.Name = request.Name.Trim();
        schedule.ReportType = request.ReportType;
        schedule.FilterSnapshotJson = request.Filter.GetRawText();
        schedule.Language = request.Language;
        schedule.Format = request.Format;
        schedule.Frequency = request.Frequency;
        schedule.IsActive = request.IsActive;
        schedule.NextRunAt = request.NextRunAt > DateTimeOffset.UtcNow
            ? request.NextRunAt
            : NextRun(DateTimeOffset.UtcNow, request.Frequency);
    }

    private static DateTimeOffset NextRun(DateTimeOffset from, ReportScheduleFrequency frequency) => frequency switch
    {
        ReportScheduleFrequency.Daily => from.AddDays(1),
        ReportScheduleFrequency.Weekly => from.AddDays(7),
        ReportScheduleFrequency.Monthly => from.AddMonths(1),
        _ => from.AddDays(1),
    };

    private static ReportScheduleResponse ToResponse(ReportSchedule entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.ReportType,
        entity.FilterSnapshotJson,
        entity.Language,
        entity.Format,
        entity.Frequency,
        entity.IsActive,
        entity.NextRunAt,
        entity.LastRunAt);

    private static ReportExportResponse ToResponse(ReportExport entity) => new(
        entity.Id,
        entity.ReportType,
        entity.Language,
        entity.Status,
        entity.ExpiresAt,
        entity.CreatedAt);

    private sealed record ReportBuildResult(ReportResponse? Report, IResult? Error);

    private sealed record ReportTicketRow(
        Guid Id,
        Guid ProjectId,
        string ProjectKey,
        string ProjectNameEnglish,
        string ProjectNameFrench,
        string TicketKey,
        TicketType Type,
        TicketPriority Priority,
        string StatusKey,
        Core.Workflows.WorkflowStatusCategory StatusCategory,
        string Assignee,
        string Reporter,
        DateTimeOffset CreatedAt,
        DateTimeOffset? FirstRespondedAt,
        DateTimeOffset? ResolvedAt,
        DateTimeOffset UpdatedAt,
        SlaState SlaState);
}

public sealed record ReportFilterRequest(
    Guid? ProjectId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    TicketPriority? Priority,
    TicketType? Type,
    Guid? AssigneeUserId,
    bool IncludeArchived);

public sealed record ReportResponse(
    string Type,
    string TitleEnglish,
    string TitleFrench,
    IReadOnlyCollection<string> Columns,
    IReadOnlyCollection<Dictionary<string, object?>> Rows,
    IReadOnlyCollection<string> DefinitionsEnglish,
    IReadOnlyCollection<string> DefinitionsFrench,
    ReportFilterRequest Filter,
    bool Truncated,
    DateTimeOffset GeneratedAt);

public sealed record CreateReportExportRequest(
    string ReportType,
    ReportFilterRequest Filter,
    AppLanguage Language);

public sealed record ReportExportResponse(
    Guid Id,
    string ReportType,
    AppLanguage Language,
    ReportExportStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record UpsertReportScheduleRequest(
    Guid? ProjectId,
    string Name,
    string ReportType,
    JsonElement Filter,
    AppLanguage Language,
    ReportExportFormat Format,
    ReportScheduleFrequency Frequency,
    bool IsActive,
    DateTimeOffset NextRunAt);

public sealed record ReportScheduleResponse(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string ReportType,
    string FilterSnapshotJson,
    AppLanguage Language,
    ReportExportFormat Format,
    ReportScheduleFrequency Frequency,
    bool IsActive,
    DateTimeOffset NextRunAt,
    DateTimeOffset? LastRunAt);

public sealed record DashboardTicketResponse(Guid Id, string Key, string Title, TicketPriority Priority, DateTimeOffset UpdatedAt);

public sealed record DashboardResponse(
    int AssignedOpen,
    int Urgent,
    int Overdue,
    int SlaRisk,
    int UnreadNotifications,
    IReadOnlyCollection<DashboardTicketResponse> RecentlyUpdated);
