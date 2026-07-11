using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects")
            .RequireAuthorization()
            .WithTags("Projects");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{projectId:guid}", GetAsync);
        group.MapPatch("/{projectId:guid}", UpdateAsync);
        group.MapPost("/{projectId:guid}/archive", ArchiveAsync);
        group.MapPost("/{projectId:guid}/restore", RestoreAsync);
        group.MapGet("/{projectId:guid}/overview", OverviewAsync);
        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyCollection<ProjectResponse>>, ProblemHttpResult>> ListAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var accessibleIds = authorization.ProjectRoles.Keys.ToArray();
        var hasAllProjectAccess = authorization.AllProjectRoles.Count > 0;
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(entity => hasAllProjectAccess || accessibleIds.Contains(entity.Id))
            .OrderBy(entity => entity.Key)
            .Select(entity => ToResponse(entity))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<ProjectResponse>>(projects);
    }

    private static async Task<Results<Created<ProjectResponse>, ProblemHttpResult>> CreateAsync(
        CreateProjectRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.IsTenantAdministrator() && !authorization.HasTenantRole(AppRole.ProjectManager))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var key = request.Key.Trim().ToUpperInvariant();
        if (key.Length is < 2 or > 12 || !key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiProblems.Validation(httpContext, "A 2-12 character project key and a name are required.");
        }

        if (await dbContext.Projects.AnyAsync(entity => entity.Key == key, cancellationToken))
        {
            return ApiProblems.Conflict(httpContext, "The project key is already in use.", "project_key_in_use");
        }

        var workflowId = request.WorkflowId ?? await dbContext.Workflows
            .Where(entity => entity.IsDefault && entity.IsActive)
            .Select(entity => entity.Id)
            .SingleAsync(cancellationToken);
        if (!await dbContext.Workflows.AnyAsync(entity => entity.Id == workflowId && entity.IsActive, cancellationToken))
        {
            return ApiProblems.Validation(httpContext, "The selected workflow is unavailable.", "invalid_workflow");
        }

        var project = new Project
        {
            TenantId = authorization.TenantId,
            Key = key,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            WorkflowId = workflowId,
            DefaultPriority = (int)request.DefaultPriority,
            DefaultAssigneeUserId = request.DefaultAssigneeUserId,
        };
        dbContext.Projects.Add(project);
        AddAudit(dbContext, httpContext, project, "project.created", "Project was created.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/projects/{project.Id}", ToResponse(project));
    }

    private static async Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> GetAsync(
        Guid projectId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.CanAccessProject(projectId))
        {
            return ApiProblems.NotFound(httpContext, "Project");
        }

        var project = await dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == projectId, cancellationToken);
        return project is null ? ApiProblems.NotFound(httpContext, "Project") : TypedResults.Ok(ToResponse(project));
    }

    private static async Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.CanManageProject(projectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(entity => entity.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ApiProblems.NotFound(httpContext, "Project");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiProblems.Validation(httpContext, "A project name is required.");
        }

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.DefaultPriority = (int)request.DefaultPriority;
        project.DefaultAssigneeUserId = request.DefaultAssigneeUserId;
        AddAudit(dbContext, httpContext, project, "project.updated", "Project settings were updated.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(project));
    }

    private static Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> ArchiveAsync(
        Guid projectId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetArchivedAsync(projectId, true, httpContext, dbContext, cancellationToken);

    private static Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> RestoreAsync(
        Guid projectId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        SetArchivedAsync(projectId, false, httpContext, dbContext, cancellationToken);

    private static async Task<Results<Ok<ProjectResponse>, ProblemHttpResult>> SetArchivedAsync(
        Guid projectId,
        bool archived,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.CanManageProject(projectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(entity => entity.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ApiProblems.NotFound(httpContext, "Project");
        }

        project.IsArchived = archived;
        AddAudit(
            dbContext,
            httpContext,
            project,
            archived ? "project.archived" : "project.restored",
            archived ? "Project was archived." : "Project was restored.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(project));
    }

    private static async Task<Results<Ok<ProjectOverviewResponse>, ProblemHttpResult>> OverviewAsync(
        Guid projectId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (!authorization.CanAccessProject(projectId) ||
            !await dbContext.Projects.AnyAsync(entity => entity.Id == projectId, cancellationToken))
        {
            return ApiProblems.NotFound(httpContext, "Project");
        }

        var now = DateTimeOffset.UtcNow;
        var tickets = dbContext.Tickets.AsNoTracking().Where(entity => entity.ProjectId == projectId && !entity.IsArchived);
        var byStatus = await tickets
            .GroupBy(entity => new
            {
                entity.WorkflowStatusId,
                entity.WorkflowStatus.Key,
                entity.WorkflowStatus.Label,
            })
            .Select(group => new ProjectStatusCount(
                group.Key.WorkflowStatusId,
                group.Key.Key,
                group.Key.Label,
                group.Count()))
            .ToArrayAsync(cancellationToken);
        var byPriority = await tickets
            .GroupBy(entity => entity.Priority)
            .Select(group => new ProjectPriorityCount(group.Key, group.Count()))
            .ToArrayAsync(cancellationToken);
        var unassigned = await tickets.CountAsync(entity => entity.AssigneeUserId == null, cancellationToken);
        var overdue = await tickets.CountAsync(entity => entity.DueAt != null && entity.DueAt < now && entity.ResolvedAt == null, cancellationToken);
        var recent = await tickets.OrderByDescending(entity => entity.UpdatedAt)
            .Take(8)
            .Select(entity => new ProjectRecentTicket(entity.Id, entity.Key, entity.Title, entity.Priority, entity.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new ProjectOverviewResponse(byStatus, byPriority, unassigned, overdue, recent));
    }

    private static void AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        Project project,
        string action,
        string summary) => dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = project.TenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = nameof(Project),
            TargetId = project.Id.ToString(),
            Summary = summary,
            CorrelationId = context.TraceIdentifier,
        });

    private static ProjectResponse ToResponse(Project entity) => new(
        entity.Id,
        entity.Key,
        entity.Name,
        entity.Description,
        entity.IsArchived,
        entity.WorkflowId,
        (TicketPriority)entity.DefaultPriority,
        entity.DefaultAssigneeUserId,
        entity.CreatedAt,
        entity.UpdatedAt);
}

public sealed record CreateProjectRequest(
    string Key,
    string Name,
    string? Description,
    Guid? WorkflowId,
    TicketPriority DefaultPriority,
    Guid? DefaultAssigneeUserId);

public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    TicketPriority DefaultPriority,
    Guid? DefaultAssigneeUserId);

public sealed record ProjectResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    bool IsArchived,
    Guid WorkflowId,
    TicketPriority DefaultPriority,
    Guid? DefaultAssigneeUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProjectStatusCount(
    Guid StatusId,
    string Key,
    string Label,
    int Count);

public sealed record ProjectPriorityCount(TicketPriority Priority, int Count);

public sealed record ProjectRecentTicket(Guid Id, string Key, string Title, TicketPriority Priority, DateTimeOffset UpdatedAt);

public sealed record ProjectOverviewResponse(
    IReadOnlyCollection<ProjectStatusCount> ByStatus,
    IReadOnlyCollection<ProjectPriorityCount> ByPriority,
    int Unassigned,
    int Overdue,
    IReadOnlyCollection<ProjectRecentTicket> RecentlyUpdated);
