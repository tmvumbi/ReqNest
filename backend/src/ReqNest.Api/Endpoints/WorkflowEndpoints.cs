using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Workflows;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workflows")
            .RequireAuthorization()
            .WithTags("Workflows");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{workflowId:guid}", UpdateAsync);
        group.MapPost("/{workflowId:guid}/copy-to-project/{projectId:guid}", CopyToProjectAsync);
        group.MapGet("/{workflowId:guid}/status-usage", GetStatusUsageAsync);

        endpoints.MapPut("/api/projects/{projectId:guid}/workflow", AssignProjectWorkflowAsync)
            .RequireAuthorization()
            .WithTags("Workflows");
        return endpoints;
    }

    private static async Task<IResult> GetStatusUsageAsync(
        Guid workflowId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.TenantAuthorization() is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var statuses = await dbContext.WorkflowStatuses.AsNoTracking()
            .Where(entity => entity.WorkflowId == workflowId)
            .Select(entity => new { entity.Id, entity.Key })
            .ToArrayAsync(cancellationToken);
        if (statuses.Length == 0)
        {
            return ApiProblems.NotFound(httpContext, "Workflow");
        }

        var statusIds = statuses.Select(status => status.Id).ToArray();
        var counts = await dbContext.Tickets.AsNoTracking()
            .Where(entity => statusIds.Contains(entity.WorkflowStatusId))
            .GroupBy(entity => entity.WorkflowStatusId)
            .Select(group => new { StatusId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var usage = statuses.ToDictionary(
            status => status.Key,
            status => counts.FirstOrDefault(count => count.StatusId == status.Id)?.Count ?? 0);
        return TypedResults.Ok(usage);
    }

    private static async Task<IResult> UpdateAsync(
        Guid workflowId,
        UpdateWorkflowRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var workflow = await dbContext.Workflows
            .Include(entity => entity.Statuses)
            .Include(entity => entity.Transitions)
            .SingleOrDefaultAsync(entity => entity.Id == workflowId, cancellationToken);
        if (workflow is null)
        {
            return ApiProblems.NotFound(httpContext, "Workflow");
        }

        if (workflow.ProjectId is null
                ? !authorization.IsTenantAdministrator()
                : !authorization.CanManageProject(workflow.ProjectId.Value))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var validationError = ValidateDefinition(request.Statuses, request.Transitions);
        if (validationError is not null)
        {
            return ApiProblems.Validation(httpContext, validationError, "invalid_workflow");
        }

        var requestedByKey = request.Statuses.ToDictionary(
            entity => entity.Key.Trim().ToUpperInvariant(),
            StringComparer.Ordinal);
        var existingByKey = workflow.Statuses.ToDictionary(entity => entity.Key, StringComparer.Ordinal);
        if (await dbContext.Workflows.AnyAsync(
                entity => entity.Id != workflow.Id && entity.Name == request.Name.Trim(),
                cancellationToken))
        {
            return ApiProblems.Conflict(httpContext, "A workflow with this name already exists.", "workflow_name_in_use");
        }
        var removed = workflow.Statuses.Where(entity => !requestedByKey.ContainsKey(entity.Key)).ToArray();
        var usedRemovedStatusIds = await dbContext.Tickets
            .Where(entity => removed.Select(status => status.Id).Contains(entity.WorkflowStatusId))
            .Select(entity => entity.WorkflowStatusId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        foreach (var statusId in usedRemovedStatusIds)
        {
            var oldKey = existingByKey.Single(entry => entry.Value.Id == statusId).Key;
            if (!request.StatusMappings.TryGetValue(oldKey, out var targetKey) ||
                !requestedByKey.ContainsKey(targetKey.Trim().ToUpperInvariant()))
            {
                return ApiProblems.Conflict(
                    httpContext,
                    "Every removed status used by an existing ticket must be mapped to a retained status.",
                    "status_mapping_required");
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var existingStatus in workflow.Statuses)
        {
            existingStatus.Order = -existingStatus.Order - 10_000;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var statusEntities = new Dictionary<string, WorkflowStatus>(StringComparer.Ordinal);
        foreach (var definition in request.Statuses)
        {
            var key = definition.Key.Trim().ToUpperInvariant();
            if (!existingByKey.TryGetValue(key, out var status))
            {
                status = new WorkflowStatus
                {
                    TenantId = workflow.TenantId,
                    WorkflowId = workflow.Id,
                    Workflow = workflow,
                    Key = key,
                };
                dbContext.WorkflowStatuses.Add(status);
            }

            status.Label = definition.Label.Trim();
            status.Category = definition.Category;
            status.Order = definition.Order;
            status.Color = definition.Color;
            status.IsInitial = definition.IsInitial;
            status.IsTerminal = definition.IsTerminal;
            statusEntities[key] = status;
        }

        foreach (var mapping in request.StatusMappings)
        {
            if (!existingByKey.TryGetValue(mapping.Key.Trim().ToUpperInvariant(), out var source) ||
                !statusEntities.TryGetValue(mapping.Value.Trim().ToUpperInvariant(), out var target) ||
                !existingByKey.ContainsKey(target.Key))
            {
                return ApiProblems.Validation(httpContext, "A status mapping is invalid.");
            }

            await dbContext.Tickets.Where(entity => entity.WorkflowStatusId == source.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.WorkflowStatusId, target.Id), cancellationToken);
        }

        dbContext.WorkflowTransitions.RemoveRange(workflow.Transitions);
        workflow.Transitions.Clear();
        dbContext.WorkflowStatuses.RemoveRange(removed);
        workflow.Statuses = statusEntities.Values.ToList();
        workflow.Name = request.Name.Trim();
        workflow.Description = request.Description?.Trim();
        workflow.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        workflow.Transitions = request.Transitions.Select(entity => new WorkflowTransition
        {
            TenantId = workflow.TenantId,
            WorkflowId = workflow.Id,
            Workflow = workflow,
            FromStatusId = statusEntities[entity.FromKey.Trim().ToUpperInvariant()].Id,
            ToStatusId = statusEntities[entity.ToKey.Trim().ToUpperInvariant()].Id,
            Name = entity.Name?.Trim(),
            CommentRequired = entity.CommentRequired,
        }).ToList();
        dbContext.WorkflowTransitions.AddRange(workflow.Transitions);
        AddAudit(dbContext, httpContext, workflow, "workflow.updated", "Workflow configuration was updated.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(workflow));
    }

    private static async Task<Results<Ok<IReadOnlyCollection<WorkflowResponse>>, ProblemHttpResult>> ListAsync(
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var accessibleProjectIds = authorization.ProjectRoles.Keys.ToArray();
        var allProjects = authorization.AllProjectRoles.Count > 0;
        var workflows = await dbContext.Workflows
            .AsNoTracking()
            .Where(entity => entity.ProjectId == null || allProjects || accessibleProjectIds.Contains(entity.ProjectId.Value))
            .Include(entity => entity.Statuses)
            .Include(entity => entity.Transitions)
            .OrderByDescending(entity => entity.IsDefault)
            .ThenBy(entity => entity.Name)
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<WorkflowResponse>>(workflows.Select(ToResponse).ToArray());
    }

    private static async Task<Results<Created<WorkflowResponse>, ProblemHttpResult>> CreateAsync(
        CreateWorkflowRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (request.ProjectId is null ? !authorization.IsTenantAdministrator() : !authorization.CanManageProject(request.ProjectId.Value))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var validationError = ValidateDefinition(request.Statuses, request.Transitions);
        if (validationError is not null)
        {
            return ApiProblems.Validation(httpContext, validationError, "invalid_workflow");
        }

        if (request.ProjectId is not null &&
            !await dbContext.Projects.AnyAsync(entity => entity.Id == request.ProjectId && !entity.IsArchived, cancellationToken))
        {
            return ApiProblems.NotFound(httpContext, "Project");
        }

        if (await dbContext.Workflows.AnyAsync(entity => entity.Name == request.Name.Trim(), cancellationToken))
        {
            return ApiProblems.Conflict(httpContext, "A workflow with this name already exists.", "workflow_name_in_use");
        }

        var workflow = BuildWorkflow(authorization.TenantId, request);
        dbContext.Workflows.Add(workflow);
        AddAudit(dbContext, httpContext, workflow, "workflow.created", "Workflow was created.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/workflows/{workflow.Id}", ToResponse(workflow));
    }

    private static async Task<Results<Created<WorkflowResponse>, ProblemHttpResult>> CopyToProjectAsync(
        Guid workflowId,
        Guid projectId,
        CopyWorkflowRequest request,
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

        var source = await dbContext.Workflows
            .AsNoTracking()
            .Include(entity => entity.Statuses)
            .Include(entity => entity.Transitions)
            .SingleOrDefaultAsync(entity => entity.Id == workflowId && entity.IsActive, cancellationToken);
        var project = await dbContext.Projects.SingleOrDefaultAsync(entity => entity.Id == projectId, cancellationToken);
        if (source is null || project is null)
        {
            return ApiProblems.NotFound(httpContext, "Workflow or project");
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? $"{project.Key} workflow" : request.Name.Trim();
        if (await dbContext.Workflows.AnyAsync(entity => entity.Name == name, cancellationToken))
        {
            return ApiProblems.Conflict(httpContext, "A workflow with this name already exists.", "workflow_name_in_use");
        }

        var createRequest = new CreateWorkflowRequest(
            name,
            source.Description,
            projectId,
            source.Statuses.OrderBy(entity => entity.Order).Select(entity => new WorkflowStatusRequest(
                entity.Key,
                entity.Label,
                entity.Category,
                entity.Order,
                entity.Color,
                entity.IsInitial,
                entity.IsTerminal)).ToArray(),
            source.Transitions.Select(entity => new WorkflowTransitionRequest(
                source.Statuses.Single(status => status.Id == entity.FromStatusId).Key,
                source.Statuses.Single(status => status.Id == entity.ToStatusId).Key,
                entity.Name,
                entity.CommentRequired)).ToArray());
        var copy = BuildWorkflow(authorization.TenantId, createRequest);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Workflows.Add(copy);
        AddAudit(dbContext, httpContext, copy, "workflow.copied", $"Workflow copied from {source.Name} for project {project.Key}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var sourceStatus in source.Statuses)
        {
            var targetStatus = copy.Statuses.Single(status => status.Key == sourceStatus.Key);
            await dbContext.Tickets.Where(entity => entity.ProjectId == project.Id && entity.WorkflowStatusId == sourceStatus.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.WorkflowStatusId, targetStatus.Id), cancellationToken);
        }

        project.WorkflowId = copy.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Created($"/api/workflows/{copy.Id}", ToResponse(copy));
    }

    private static async Task<Results<Ok<ProjectWorkflowAssignmentResponse>, ProblemHttpResult>> AssignProjectWorkflowAsync(
        Guid projectId,
        AssignProjectWorkflowRequest request,
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
        var workflow = await dbContext.Workflows
            .Include(entity => entity.Statuses)
            .SingleOrDefaultAsync(entity => entity.Id == request.WorkflowId && entity.IsActive, cancellationToken);
        if (project is null || workflow is null || workflow.ProjectId is not null && workflow.ProjectId != projectId)
        {
            return ApiProblems.NotFound(httpContext, "Workflow or project");
        }

        var ticketCounts = await dbContext.Tickets
            .Where(entity => entity.ProjectId == projectId)
            .GroupBy(entity => entity.WorkflowStatusId)
            .Select(group => new { StatusId = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var targetStatusIds = workflow.Statuses.Select(entity => entity.Id).ToHashSet();
        var missingMappings = ticketCounts
            .Where(item => !targetStatusIds.Contains(item.StatusId) && !request.StatusMappings.ContainsKey(item.StatusId))
            .ToArray();
        if (missingMappings.Length > 0)
        {
            return ApiProblems.Conflict(
                httpContext,
                "Every status used by an existing ticket must be mapped before changing workflows.",
                "status_mapping_required");
        }

        if (request.StatusMappings.Values.Any(value => !targetStatusIds.Contains(value)))
        {
            return ApiProblems.Validation(httpContext, "A status mapping targets a status outside the selected workflow.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var mapping in request.StatusMappings)
        {
            await dbContext.Tickets
                .Where(entity => entity.ProjectId == projectId && entity.WorkflowStatusId == mapping.Key)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entity => entity.WorkflowStatusId, mapping.Value), cancellationToken);
        }

        project.WorkflowId = workflow.Id;
        AddAudit(dbContext, httpContext, workflow, "project.workflow.assigned", $"Workflow assigned to project {project.Key}.");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(new ProjectWorkflowAssignmentResponse(
            project.Id,
            workflow.Id,
            ticketCounts.Sum(item => item.Count)));
    }

    private static string? ValidateDefinition(
        IReadOnlyCollection<WorkflowStatusRequest> statuses,
        IReadOnlyCollection<WorkflowTransitionRequest> transitions)
    {
        if (statuses.Count < 2 || statuses.Count(entity => entity.IsInitial) != 1 ||
            !statuses.Any(entity => !entity.IsTerminal) || !statuses.Any(entity => entity.IsTerminal))
        {
            return "A workflow needs one initial status, at least one non-terminal status, and at least one terminal status.";
        }

        var keys = statuses.Select(entity => entity.Key.Trim().ToUpperInvariant()).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
            statuses.Select(entity => entity.Order).Distinct().Count() != statuses.Count ||
            statuses.Any(entity => string.IsNullOrWhiteSpace(entity.Label)))
        {
            return "Status keys and order values must be unique and localized labels are required.";
        }

        if (transitions.Any(entity => !keys.Contains(entity.FromKey.Trim().ToUpperInvariant()) ||
                                      !keys.Contains(entity.ToKey.Trim().ToUpperInvariant()) ||
                                      entity.FromKey.Equals(entity.ToKey, StringComparison.OrdinalIgnoreCase)))
        {
            return "Every transition must connect two distinct statuses in the workflow.";
        }

        return null;
    }

    private static Workflow BuildWorkflow(Guid tenantId, CreateWorkflowRequest request)
    {
        var workflow = new Workflow
        {
            TenantId = tenantId,
            ProjectId = request.ProjectId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
        };
        var statuses = request.Statuses.ToDictionary(
            entity => entity.Key.Trim().ToUpperInvariant(),
            entity => new WorkflowStatus
            {
                TenantId = tenantId,
                WorkflowId = workflow.Id,
                Workflow = workflow,
                Key = entity.Key.Trim().ToUpperInvariant(),
                Label = entity.Label.Trim(),
                Category = entity.Category,
                Order = entity.Order,
                Color = entity.Color,
                IsInitial = entity.IsInitial,
                IsTerminal = entity.IsTerminal,
            });
        workflow.Statuses = statuses.Values.ToArray();
        workflow.Transitions = request.Transitions.Select(entity => new WorkflowTransition
        {
            TenantId = tenantId,
            WorkflowId = workflow.Id,
            Workflow = workflow,
            FromStatusId = statuses[entity.FromKey.Trim().ToUpperInvariant()].Id,
            FromStatus = statuses[entity.FromKey.Trim().ToUpperInvariant()],
            ToStatusId = statuses[entity.ToKey.Trim().ToUpperInvariant()].Id,
            ToStatus = statuses[entity.ToKey.Trim().ToUpperInvariant()],
            Name = entity.Name?.Trim(),
            CommentRequired = entity.CommentRequired,
        }).ToArray();
        return workflow;
    }

    private static void AddAudit(
        ReqNestDbContext dbContext,
        HttpContext context,
        Workflow workflow,
        string action,
        string summary) => dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = workflow.TenantId,
            ActorUserId = context.User.UserId(),
            Action = action,
            TargetType = nameof(Workflow),
            TargetId = workflow.Id.ToString(),
            Summary = summary,
            CorrelationId = context.TraceIdentifier,
        });

    private static WorkflowResponse ToResponse(Workflow entity) => new(
        entity.Id,
        entity.Name,
        entity.Description,
        entity.ProjectId,
        entity.IsDefault,
        entity.IsActive,
        entity.Statuses.OrderBy(status => status.Order).Select(status => new WorkflowStatusResponse(
            status.Id,
            status.Key,
            status.Label,
            status.Category,
            status.Order,
            status.Color,
            status.IsInitial,
            status.IsTerminal)).ToArray(),
        entity.Transitions.Select(transition => new WorkflowTransitionResponse(
            transition.Id,
            transition.FromStatusId,
            transition.ToStatusId,
            transition.Name,
            transition.CommentRequired)).ToArray());
}

public sealed record WorkflowStatusRequest(
    string Key,
    string Label,
    WorkflowStatusCategory Category,
    int Order,
    string Color,
    bool IsInitial,
    bool IsTerminal);

public sealed record WorkflowTransitionRequest(
    string FromKey,
    string ToKey,
    string? Name,
    bool CommentRequired);

public sealed record CreateWorkflowRequest(
    string Name,
    string? Description,
    Guid? ProjectId,
    IReadOnlyCollection<WorkflowStatusRequest> Statuses,
    IReadOnlyCollection<WorkflowTransitionRequest> Transitions);

public sealed record CopyWorkflowRequest(string? Name);

public sealed record UpdateWorkflowRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<WorkflowStatusRequest> Statuses,
    IReadOnlyCollection<WorkflowTransitionRequest> Transitions,
    IReadOnlyDictionary<string, string> StatusMappings);

public sealed record AssignProjectWorkflowRequest(
    Guid WorkflowId,
    IReadOnlyDictionary<Guid, Guid> StatusMappings);

public sealed record ProjectWorkflowAssignmentResponse(Guid ProjectId, Guid WorkflowId, int MigratedTicketCount);

public sealed record WorkflowStatusResponse(
    Guid Id,
    string Key,
    string Label,
    WorkflowStatusCategory Category,
    int Order,
    string Color,
    bool IsInitial,
    bool IsTerminal);

public sealed record WorkflowTransitionResponse(
    Guid Id,
    Guid FromStatusId,
    Guid ToStatusId,
    string? Name,
    bool CommentRequired);

public sealed record WorkflowResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? ProjectId,
    bool IsDefault,
    bool IsActive,
    IReadOnlyCollection<WorkflowStatusResponse> Statuses,
    IReadOnlyCollection<WorkflowTransitionResponse> Transitions);
