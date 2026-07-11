using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Configuration;
using ReqNest.Core.Identity;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var schema = endpoints.MapGroup("/api/configuration/ticket-schema")
            .RequireAuthorization()
            .WithTags("Ticket schema");
        schema.MapGet("/", GetSchemaAsync);
        schema.MapPost("/types", CreateTypeAsync);
        schema.MapPut("/types/{definitionId:guid}", UpdateTypeAsync);
        schema.MapDelete("/types/{definitionId:guid}", DeleteTypeAsync);
        schema.MapPost("/priorities", CreatePriorityAsync);
        schema.MapPut("/priorities/{definitionId:guid}", UpdatePriorityAsync);
        schema.MapDelete("/priorities/{definitionId:guid}", DeletePriorityAsync);
        schema.MapPost("/custom-fields", CreateCustomFieldAsync);
        schema.MapPut("/custom-fields/{definitionId:guid}", UpdateCustomFieldAsync);

        var sla = endpoints.MapGroup("/api/configuration/sla-policies")
            .RequireAuthorization()
            .WithTags("SLA policies");
        sla.MapGet("/", ListSlaPoliciesAsync);
        sla.MapPost("/", CreateSlaPolicyAsync);
        sla.MapPut("/{policyId:guid}", UpdateSlaPolicyAsync);
        sla.MapPut("/{policyId:guid}/projects/{projectId:guid}", AssignProjectPolicyAsync);
        return endpoints;
    }

    private static async Task<IResult> GetSchemaAsync(
        Guid? projectId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (projectId is not null && !authorization.CanAccessProject(projectId.Value))
        {
            return ApiProblems.NotFound(httpContext, "Project");
        }

        var types = await dbContext.TicketTypeDefinitions.AsNoTracking()
            .Where(entity => entity.ProjectId == null || entity.ProjectId == projectId)
            .OrderByDescending(entity => entity.ProjectId == projectId)
            .ThenBy(entity => entity.Order)
            .Select(entity => new TicketTypeDefinitionResponse(
                entity.Id,
                entity.ProjectId,
                entity.Key,
                entity.Label,
                entity.Order,
                entity.IsActive))
            .ToArrayAsync(cancellationToken);
        var priorities = await dbContext.TicketPriorityDefinitions.AsNoTracking()
            .Where(entity => entity.ProjectId == null || entity.ProjectId == projectId)
            .OrderByDescending(entity => entity.ProjectId == projectId)
            .ThenBy(entity => entity.Order)
            .Select(entity => new TicketPriorityDefinitionResponse(
                entity.Id,
                entity.ProjectId,
                entity.Key,
                entity.Label,
                entity.Color,
                entity.Weight,
                entity.Order,
                entity.IsActive))
            .ToArrayAsync(cancellationToken);
        var fields = await dbContext.CustomFieldDefinitions.AsNoTracking()
            .Where(entity => entity.ProjectIds.Length == 0 ||
                             projectId != null && entity.ProjectIds.Contains(projectId.Value))
            .OrderByDescending(entity => entity.ProjectIds.Length > 0)
            .ThenBy(entity => entity.Order)
            .Select(entity => new CustomFieldDefinitionResponse(
                entity.Id,
                entity.ProjectIds,
                entity.Key,
                entity.Label,
                entity.Kind,
                entity.IsRequired,
                entity.IsActive,
                entity.Order,
                entity.OptionsJson))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok(new TicketSchemaResponse(types, priorities, fields));
    }

    private static async Task<IResult> CreateTypeAsync(
        UpsertTicketTypeDefinitionRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = ValidateScopeAndDefinition(
            authorization,
            request.ProjectId,
            request.Key,
            request.Label,
            httpContext);
        if (error is not null)
        {
            return error;
        }

        var entity = new TicketTypeDefinition { TenantId = authorization!.TenantId };
        Apply(entity, request);
        dbContext.TicketTypeDefinitions.Add(entity);
        AddAudit(dbContext, httpContext, authorization.TenantId, "ticket_schema.type.created", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/configuration/ticket-schema/types/{entity.Id}", ToResponse(entity));
    }

    private static async Task<IResult> UpdateTypeAsync(
        Guid definitionId,
        UpsertTicketTypeDefinitionRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var entity = await dbContext.TicketTypeDefinitions.SingleOrDefaultAsync(
            item => item.Id == definitionId,
            cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound(httpContext, "Ticket type");
        }

        var error = ValidateScopeAndDefinition(
            authorization,
            entity.ProjectId,
            entity.Key,
            request.Label,
            httpContext);
        if (error is not null || request.ProjectId != entity.ProjectId || request.Key != entity.Key)
        {
            return error ?? ApiProblems.Validation(httpContext, "The key and scope cannot change after creation.");
        }

        Apply(entity, request);
        AddAudit(dbContext, httpContext, authorization!.TenantId, "ticket_schema.type.updated", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(entity));
    }

    private static async Task<IResult> DeleteTypeAsync(
        Guid definitionId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var entity = await dbContext.TicketTypeDefinitions.SingleOrDefaultAsync(
            item => item.Id == definitionId,
            cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound(httpContext, "Ticket type");
        }

        var error = ValidateScope(authorization, entity.ProjectId, httpContext);
        if (error is not null)
        {
            return error;
        }

        var key = entity.Key;
        var inUse = await dbContext.Tickets.AsNoTracking().AnyAsync(
            ticket => ticket.TypeKey == key &&
                      (entity.ProjectId == null || ticket.ProjectId == entity.ProjectId),
            cancellationToken);
        if (inUse)
        {
            return ApiProblems.Conflict(
                httpContext, "This ticket type is used by existing tickets and cannot be deleted.", "definition_in_use");
        }

        dbContext.TicketTypeDefinitions.Remove(entity);
        AddAudit(dbContext, httpContext, authorization!.TenantId, "ticket_schema.type.deleted", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeletePriorityAsync(
        Guid definitionId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var entity = await dbContext.TicketPriorityDefinitions.SingleOrDefaultAsync(
            item => item.Id == definitionId,
            cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound(httpContext, "Ticket priority");
        }

        var error = ValidateScope(authorization, entity.ProjectId, httpContext);
        if (error is not null)
        {
            return error;
        }

        var key = entity.Key;
        var inUse = await dbContext.Tickets.AsNoTracking().AnyAsync(
            ticket => ticket.PriorityKey == key &&
                      (entity.ProjectId == null || ticket.ProjectId == entity.ProjectId),
            cancellationToken);
        if (inUse)
        {
            return ApiProblems.Conflict(
                httpContext, "This priority is used by existing tickets and cannot be deleted.", "definition_in_use");
        }

        dbContext.TicketPriorityDefinitions.Remove(entity);
        AddAudit(dbContext, httpContext, authorization!.TenantId, "ticket_schema.priority.deleted", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> CreatePriorityAsync(
        UpsertTicketPriorityDefinitionRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = ValidateScopeAndDefinition(
            authorization,
            request.ProjectId,
            request.Key,
            request.Label,
            httpContext);
        if (error is not null || request.Weight is < 0 or > 100 || !ValidColor(request.Color))
        {
            return error ?? ApiProblems.Validation(httpContext, "Priority weight or color is invalid.");
        }

        var entity = new TicketPriorityDefinition { TenantId = authorization!.TenantId };
        Apply(entity, request);
        dbContext.TicketPriorityDefinitions.Add(entity);
        AddAudit(dbContext, httpContext, authorization.TenantId, "ticket_schema.priority.created", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/configuration/ticket-schema/priorities/{entity.Id}", ToResponse(entity));
    }

    private static async Task<IResult> UpdatePriorityAsync(
        Guid definitionId,
        UpsertTicketPriorityDefinitionRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var entity = await dbContext.TicketPriorityDefinitions.SingleOrDefaultAsync(
            item => item.Id == definitionId,
            cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound(httpContext, "Priority");
        }

        var error = ValidateScopeAndDefinition(
            authorization,
            entity.ProjectId,
            entity.Key,
            request.Label,
            httpContext);
        if (error is not null || request.ProjectId != entity.ProjectId || request.Key != entity.Key ||
            request.Weight is < 0 or > 100 || !ValidColor(request.Color))
        {
            return error ?? ApiProblems.Validation(httpContext, "The priority definition is invalid.");
        }

        Apply(entity, request);
        AddAudit(dbContext, httpContext, authorization!.TenantId, "ticket_schema.priority.updated", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(entity));
    }

    private static async Task<IResult> CreateCustomFieldAsync(
        UpsertCustomFieldDefinitionRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = ValidateCustomField(request, authorization, httpContext);
        if (error is not null)
        {
            return error;
        }

        var entity = new CustomFieldDefinition { TenantId = authorization!.TenantId };
        Apply(entity, request);
        dbContext.CustomFieldDefinitions.Add(entity);
        AddAudit(dbContext, httpContext, authorization.TenantId, "ticket_schema.custom_field.created", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/configuration/ticket-schema/custom-fields/{entity.Id}", ToResponse(entity));
    }

    private static async Task<IResult> UpdateCustomFieldAsync(
        Guid definitionId,
        UpsertCustomFieldDefinitionRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var entity = await dbContext.CustomFieldDefinitions.SingleOrDefaultAsync(
            item => item.Id == definitionId,
            cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound(httpContext, "Custom field");
        }

        var error = ValidateCustomField(request, authorization, httpContext);
        if (error is not null || request.Key != entity.Key || request.Kind != entity.Kind)
        {
            return error ?? ApiProblems.Validation(httpContext, "The key and type cannot change after creation.");
        }

        Apply(entity, request);
        AddAudit(dbContext, httpContext, authorization!.TenantId, "ticket_schema.custom_field.updated", entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(entity));
    }

    private static async Task<IResult> ListSlaPoliciesAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var allowedProjectIds = authorization.ProjectRoles.Keys
            .Concat(authorization.ProjectPermissions.Keys)
            .ToArray();
        var policies = await dbContext.SlaPolicies.AsNoTracking()
            .AsSplitQuery()
            .Where(entity =>
                authorization.AllProjectRoles.Count > 0 ||
                authorization.AllProjectPermissions.Count > 0 ||
                entity.ProjectIds.Length == 0 ||
                entity.ProjectIds.Any(id => allowedProjectIds.Contains(id)))
            .Include(entity => entity.Targets)
            .Include(entity => entity.Holidays)
            .OrderByDescending(entity => entity.IsDefault)
            .ThenBy(entity => entity.Name)
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<SlaPolicyResponse>>(policies.Select(ToResponse).ToArray());
    }

    private static async Task<IResult> CreateSlaPolicyAsync(
        UpsertSlaPolicyRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = ValidateSla(request, authorization, httpContext);
        if (error is not null)
        {
            return error;
        }

        var policy = new SlaPolicy { TenantId = authorization!.TenantId };
        Apply(policy, request);
        if (policy.IsDefault)
        {
            await dbContext.SlaPolicies.Where(entity => entity.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.IsDefault, false), cancellationToken);
        }

        dbContext.SlaPolicies.Add(policy);
        AddAudit(dbContext, httpContext, authorization.TenantId, "sla_policy.created", policy.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/configuration/sla-policies/{policy.Id}", ToResponse(policy));
    }

    private static async Task<IResult> UpdateSlaPolicyAsync(
        Guid policyId,
        UpsertSlaPolicyRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var error = ValidateSla(request, authorization, httpContext);
        if (error is not null)
        {
            return error;
        }

        var policy = await dbContext.SlaPolicies.AsSplitQuery()
            .Include(entity => entity.Targets)
            .Include(entity => entity.Holidays)
            .SingleOrDefaultAsync(entity => entity.Id == policyId, cancellationToken);
        if (policy is null)
        {
            return ApiProblems.NotFound(httpContext, "SLA policy");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (request.IsDefault)
        {
            await dbContext.SlaPolicies.Where(entity => entity.Id != policyId && entity.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.IsDefault, false), cancellationToken);
        }

        dbContext.SlaPriorityTargets.RemoveRange(policy.Targets);
        dbContext.SlaHolidays.RemoveRange(policy.Holidays);
        Apply(policy, request);
        AddAudit(dbContext, httpContext, authorization!.TenantId, "sla_policy.updated", policy.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(policy));
    }

    private static async Task<IResult> AssignProjectPolicyAsync(
        Guid policyId,
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

        if (!authorization.CanManageProject(projectId))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(entity => entity.Id == projectId, cancellationToken);
        var policy = await dbContext.SlaPolicies.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == policyId && entity.IsActive, cancellationToken);
        if (project is null || policy is null ||
            policy.ProjectIds.Length > 0 && !policy.ProjectIds.Contains(projectId))
        {
            return ApiProblems.NotFound(httpContext, "Project or SLA policy");
        }

        project.SlaPolicyId = policy.Id;
        AddAudit(dbContext, httpContext, authorization.TenantId, "project.sla_policy.assigned", project.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static IResult? ValidateScope(
        TenantAuthorization? authorization,
        Guid? projectId,
        HttpContext context)
    {
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        return (projectId is null ? !authorization.IsTenantAdministrator() : !authorization.CanManageProject(projectId.Value))
            ? ApiProblems.Forbidden(context)
            : null;
    }

    private static IResult? ValidateScopeAndDefinition(
        TenantAuthorization? authorization,
        Guid? projectId,
        string key,
        string label,
        HttpContext context)
    {
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        if (projectId is null ? !authorization.IsTenantAdministrator() : !authorization.CanManageProject(projectId.Value))
        {
            return ApiProblems.Forbidden(context);
        }

        return ValidateDefinition(key, label, context);
    }

    private static IResult? ValidateDefinition(string key, string label, HttpContext context) =>
        string.IsNullOrWhiteSpace(key) || key.Trim().Length > 80 ||
        !System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$") ||
        string.IsNullOrWhiteSpace(label) || label.Trim().Length > 120
            ? ApiProblems.Validation(context, "A stable key and a label are required.")
            : null;

    private static IResult? ValidateProjectSet(
        TenantAuthorization? authorization,
        IReadOnlyCollection<Guid> projectIds,
        HttpContext context)
    {
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(context);
        }

        var allowed = projectIds.Count == 0
            ? authorization.IsTenantAdministrator()
            : projectIds.All(authorization.CanManageProject);
        return allowed ? null : ApiProblems.Forbidden(context);
    }

    private static IResult? ValidateCustomField(
        UpsertCustomFieldDefinitionRequest request,
        TenantAuthorization? authorization,
        HttpContext context)
    {
        var error = ValidateProjectSet(authorization, request.ProjectIds, context) ??
                    ValidateDefinition(request.Key, request.Label, context);
        if (error is not null)
        {
            return error;
        }

        if (request.Kind == CustomFieldKind.Choice &&
            (request.Options.ValueKind != JsonValueKind.Array ||
             request.Options.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String)))
        {
            return ApiProblems.Validation(context, "Choice fields require a string option array.");
        }

        return null;
    }

    private static IResult? ValidateSla(
        UpsertSlaPolicyRequest request,
        TenantAuthorization? authorization,
        HttpContext context)
    {
        var scopeError = ValidateProjectSet(authorization, request.ProjectIds, context);
        if (scopeError is not null)
        {
            return scopeError;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return ApiProblems.Validation(context, "The SLA time zone is not available.");
        }
        catch (InvalidTimeZoneException)
        {
            return ApiProblems.Validation(context, "The SLA time zone is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160 ||
            request.WorkingDaysMask is < 1 or > 127 ||
            request.BusinessDayStartMinutes is < 0 or >= 1440 ||
            request.BusinessDayEndMinutes <= request.BusinessDayStartMinutes ||
            request.BusinessDayEndMinutes > 1440 ||
            request.WarningMinutesBefore < 0 || request.Targets.Count == 0 ||
            request.Targets.Any(target => string.IsNullOrWhiteSpace(target.PriorityKey) ||
                                          target.FirstResponseMinutes <= 0 || target.ResolutionMinutes <= 0))
        {
            return ApiProblems.Validation(context, "The SLA calendar and targets are invalid.");
        }

        return null;
    }

    private static void Apply(TicketTypeDefinition entity, UpsertTicketTypeDefinitionRequest request)
    {
        entity.ProjectId = request.ProjectId;
        entity.Key = request.Key.Trim();
        entity.Label = request.Label.Trim();
        entity.Order = request.Order;
        entity.IsActive = request.IsActive;
    }

    private static void Apply(TicketPriorityDefinition entity, UpsertTicketPriorityDefinitionRequest request)
    {
        entity.ProjectId = request.ProjectId;
        entity.Key = request.Key.Trim();
        entity.Label = request.Label.Trim();
        entity.Color = request.Color;
        entity.Weight = request.Weight;
        entity.Order = request.Order;
        entity.IsActive = request.IsActive;
    }

    private static void Apply(CustomFieldDefinition entity, UpsertCustomFieldDefinitionRequest request)
    {
        entity.ProjectIds = request.ProjectIds.Distinct().ToArray();
        entity.Key = request.Key.Trim();
        entity.Label = request.Label.Trim();
        entity.Kind = request.Kind;
        entity.IsRequired = request.IsRequired;
        entity.IsActive = request.IsActive;
        entity.Order = request.Order;
        entity.OptionsJson = request.Kind == CustomFieldKind.Choice ? request.Options.GetRawText() : "[]";
    }

    private static void Apply(SlaPolicy entity, UpsertSlaPolicyRequest request)
    {
        entity.ProjectIds = request.ProjectIds.Distinct().ToArray();
        entity.Name = request.Name.Trim();
        entity.TimeZone = request.TimeZone;
        entity.IsDefault = request.IsDefault;
        entity.IsActive = request.IsActive;
        entity.WorkingDaysMask = request.WorkingDaysMask;
        entity.BusinessDayStartMinutes = request.BusinessDayStartMinutes;
        entity.BusinessDayEndMinutes = request.BusinessDayEndMinutes;
        entity.WarningMinutesBefore = request.WarningMinutesBefore;
        entity.PauseStatusKeys = request.PauseStatusKeys.Select(key => key.Trim()).Where(key => key.Length > 0)
            .Distinct(StringComparer.Ordinal).ToArray();
        entity.Targets = request.Targets.Select(target => new SlaPriorityTarget
        {
            TenantId = entity.TenantId,
            SlaPolicyId = entity.Id,
            SlaPolicy = entity,
            PriorityKey = target.PriorityKey.Trim(),
            FirstResponseMinutes = target.FirstResponseMinutes,
            ResolutionMinutes = target.ResolutionMinutes,
        }).ToArray();
        entity.Holidays = request.Holidays.Select(holiday => new SlaHoliday
        {
            TenantId = entity.TenantId,
            SlaPolicyId = entity.Id,
            SlaPolicy = entity,
            Date = holiday.Date,
            Name = holiday.Name.Trim()[..Math.Min(holiday.Name.Trim().Length, 160)],
        }).ToArray();
    }

    private static void AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        Guid tenantId,
        string action,
        Guid targetId) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = "Configuration",
            TargetId = targetId.ToString(),
            Summary = "Operational configuration changed.",
            CorrelationId = context.TraceIdentifier,
        });

    private static bool ValidColor(string color) =>
        System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$");

    private static TicketTypeDefinitionResponse ToResponse(TicketTypeDefinition entity) => new(
        entity.Id, entity.ProjectId, entity.Key, entity.Label, entity.Order, entity.IsActive);

    private static TicketPriorityDefinitionResponse ToResponse(TicketPriorityDefinition entity) => new(
        entity.Id, entity.ProjectId, entity.Key, entity.Label, entity.Color,
        entity.Weight, entity.Order, entity.IsActive);

    private static CustomFieldDefinitionResponse ToResponse(CustomFieldDefinition entity) => new(
        entity.Id, entity.ProjectIds, entity.Key, entity.Label, entity.Kind,
        entity.IsRequired, entity.IsActive, entity.Order, entity.OptionsJson);

    private static SlaPolicyResponse ToResponse(SlaPolicy entity) => new(
        entity.Id,
        entity.ProjectIds,
        entity.Name,
        entity.TimeZone,
        entity.IsDefault,
        entity.IsActive,
        entity.WorkingDaysMask,
        entity.BusinessDayStartMinutes,
        entity.BusinessDayEndMinutes,
        entity.WarningMinutesBefore,
        entity.PauseStatusKeys,
        entity.Targets.OrderBy(target => target.PriorityKey).Select(target => new SlaTargetResponse(
            target.PriorityKey,
            target.FirstResponseMinutes,
            target.ResolutionMinutes)).ToArray(),
        entity.Holidays.OrderBy(holiday => holiday.Date).Select(holiday => new SlaHolidayResponse(
            holiday.Date,
            holiday.Name)).ToArray());
}

public sealed record UpsertTicketTypeDefinitionRequest(
    Guid? ProjectId,
    string Key,
    string Label,
    int Order,
    bool IsActive);

public sealed record TicketTypeDefinitionResponse(
    Guid Id,
    Guid? ProjectId,
    string Key,
    string Label,
    int Order,
    bool IsActive);

public sealed record UpsertTicketPriorityDefinitionRequest(
    Guid? ProjectId,
    string Key,
    string Label,
    string Color,
    int Weight,
    int Order,
    bool IsActive);

public sealed record TicketPriorityDefinitionResponse(
    Guid Id,
    Guid? ProjectId,
    string Key,
    string Label,
    string Color,
    int Weight,
    int Order,
    bool IsActive);

public sealed record UpsertCustomFieldDefinitionRequest(
    IReadOnlyCollection<Guid> ProjectIds,
    string Key,
    string Label,
    CustomFieldKind Kind,
    bool IsRequired,
    bool IsActive,
    int Order,
    JsonElement Options);

public sealed record CustomFieldDefinitionResponse(
    Guid Id,
    IReadOnlyCollection<Guid> ProjectIds,
    string Key,
    string Label,
    CustomFieldKind Kind,
    bool IsRequired,
    bool IsActive,
    int Order,
    string OptionsJson);

public sealed record TicketSchemaResponse(
    IReadOnlyCollection<TicketTypeDefinitionResponse> Types,
    IReadOnlyCollection<TicketPriorityDefinitionResponse> Priorities,
    IReadOnlyCollection<CustomFieldDefinitionResponse> CustomFields);

public sealed record UpsertSlaPolicyRequest(
    IReadOnlyCollection<Guid> ProjectIds,
    string Name,
    string TimeZone,
    bool IsDefault,
    bool IsActive,
    int WorkingDaysMask,
    int BusinessDayStartMinutes,
    int BusinessDayEndMinutes,
    int WarningMinutesBefore,
    IReadOnlyCollection<string> PauseStatusKeys,
    IReadOnlyCollection<SlaTargetRequest> Targets,
    IReadOnlyCollection<SlaHolidayRequest> Holidays);

public sealed record SlaTargetRequest(string PriorityKey, int FirstResponseMinutes, int ResolutionMinutes);

public sealed record SlaHolidayRequest(DateOnly Date, string Name);

public sealed record SlaPolicyResponse(
    Guid Id,
    IReadOnlyCollection<Guid> ProjectIds,
    string Name,
    string TimeZone,
    bool IsDefault,
    bool IsActive,
    int WorkingDaysMask,
    int BusinessDayStartMinutes,
    int BusinessDayEndMinutes,
    int WarningMinutesBefore,
    IReadOnlyCollection<string> PauseStatusKeys,
    IReadOnlyCollection<SlaTargetResponse> Targets,
    IReadOnlyCollection<SlaHolidayResponse> Holidays);

public sealed record SlaTargetResponse(string PriorityKey, int FirstResponseMinutes, int ResolutionMinutes);

public sealed record SlaHolidayResponse(DateOnly Date, string Name);
