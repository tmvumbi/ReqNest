using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Tickets;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class RelationshipEndpoints
{
    public static IEndpointRouteBuilder MapRelationshipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tickets/{ticketId:guid}")
            .RequireAuthorization()
            .WithTags("Ticket relationships");
        group.MapGet("/relationships", ListAsync);
        group.MapPost("/relationships", CreateAsync);
        group.MapDelete("/relationships/{relationshipId:guid}", DeleteAsync);
        group.MapPut("/parent", SetParentAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid ticketId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var ticketProjectId = await ProjectIdAsync(ticketId, dbContext, cancellationToken);
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (ticketProjectId is null || !authorization.CanAccessProject(ticketProjectId.Value))
        {
            return ApiProblems.NotFound(httpContext, "Ticket");
        }

        var relationships = await dbContext.TicketRelationships.AsNoTracking()
            .Where(entity => entity.SourceTicketId == ticketId || entity.TargetTicketId == ticketId)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(200)
            .ToArrayAsync(cancellationToken);
        var relatedIds = relationships.Select(entity => entity.SourceTicketId == ticketId
                ? entity.TargetTicketId
                : entity.SourceTicketId)
            .Distinct()
            .ToArray();
        var relatedTickets = await dbContext.Tickets.AsNoTracking()
            .Where(entity => relatedIds.Contains(entity.Id))
            .Select(entity => new { entity.Id, entity.ProjectId, entity.Key, entity.Title })
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);
        var response = relationships.Select(entity =>
            {
                var relatedId = entity.SourceTicketId == ticketId ? entity.TargetTicketId : entity.SourceTicketId;
                var direction = entity.SourceTicketId == ticketId ? "outgoing" : "incoming";
                return relatedTickets.TryGetValue(relatedId, out var related) && authorization.CanAccessProject(related.ProjectId)
                    ? new TicketRelationshipResponse(entity.Id, related.Id, related.Key, related.Title, entity.Type, direction, entity.CreatedAt)
                    : null;
            })
            .OfType<TicketRelationshipResponse>()
            .ToArray();
        return TypedResults.Ok<IReadOnlyCollection<TicketRelationshipResponse>>(response);
    }

    private static async Task<IResult> CreateAsync(
        Guid ticketId,
        CreateTicketRelationshipRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (ticketId == request.TargetTicketId)
        {
            return ApiProblems.Validation(httpContext, "A ticket cannot relate to itself.");
        }

        var sourceProjectId = await ProjectIdAsync(ticketId, dbContext, cancellationToken);
        var targetProjectId = await ProjectIdAsync(request.TargetTicketId, dbContext, cancellationToken);
        if (sourceProjectId is null || targetProjectId is null ||
            !authorization.CanAccessProject(targetProjectId.Value))
        {
            return ApiProblems.NotFound(httpContext, "Ticket");
        }

        if (!authorization.CanMaintainTickets(sourceProjectId.Value))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        var entity = new TicketRelationship
        {
            TenantId = authorization.TenantId,
            SourceTicketId = ticketId,
            TargetTicketId = request.TargetTicketId,
            Type = request.Type,
            CreatedByUserId = httpContext.User.UserId(),
        };
        dbContext.TicketRelationships.Add(entity);
        var source = await dbContext.Tickets.SingleAsync(item => item.Id == ticketId, cancellationToken);
        TicketEndpoints.AddAudit(dbContext, httpContext, source, "ticket.relationship.created", "A ticket relationship was created.");
        await dbContext.SaveChangesAsync(cancellationToken);
        var target = await dbContext.Tickets.AsNoTracking().SingleAsync(item => item.Id == request.TargetTicketId, cancellationToken);
        return TypedResults.Created(
            $"/api/tickets/{ticketId}/relationships/{entity.Id}",
            new TicketRelationshipResponse(entity.Id, target.Id, target.Key, target.Title, entity.Type, "outgoing", entity.CreatedAt));
    }

    private static async Task<IResult> DeleteAsync(
        Guid ticketId,
        Guid relationshipId,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        var sourceProjectId = await ProjectIdAsync(ticketId, dbContext, cancellationToken);
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        if (sourceProjectId is null || !authorization.CanMaintainTickets(sourceProjectId.Value))
        {
            return ApiProblems.NotFound(httpContext, "Relationship");
        }

        var entity = await dbContext.TicketRelationships.SingleOrDefaultAsync(
            item => item.Id == relationshipId && (item.SourceTicketId == ticketId || item.TargetTicketId == ticketId),
            cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound(httpContext, "Relationship");
        }

        dbContext.TicketRelationships.Remove(entity);
        var ticket = await dbContext.Tickets.SingleAsync(item => item.Id == ticketId, cancellationToken);
        TicketEndpoints.AddAudit(dbContext, httpContext, ticket, "ticket.relationship.deleted", "A ticket relationship was deleted.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SetParentAsync(
        Guid ticketId,
        SetParentTicketRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(entity => entity.Id == ticketId, cancellationToken);
        if (ticket is null || !authorization.CanMaintainTickets(ticket.ProjectId))
        {
            return ApiProblems.NotFound(httpContext, "Ticket");
        }

        if (request.ParentTicketId == ticketId)
        {
            return ApiProblems.Validation(httpContext, "A ticket cannot be its own parent.");
        }

        if (request.ParentTicketId is not null)
        {
            var parent = await dbContext.Tickets.AsNoTracking().SingleOrDefaultAsync(
                entity => entity.Id == request.ParentTicketId,
                cancellationToken);
            if (parent is null || !authorization.CanAccessProject(parent.ProjectId))
            {
                return ApiProblems.NotFound(httpContext, "Parent ticket");
            }

            var cursor = parent.ParentTicketId;
            for (var guard = 0; cursor is not null && guard < 100; guard++)
            {
                if (cursor == ticketId)
                {
                    return ApiProblems.Conflict(httpContext, "The parent assignment would create a cycle.", "ticket_hierarchy_cycle");
                }

                cursor = await dbContext.Tickets.AsNoTracking()
                    .Where(entity => entity.Id == cursor)
                    .Select(entity => entity.ParentTicketId)
                    .SingleOrDefaultAsync(cancellationToken);
            }
        }

        ticket.ParentTicketId = request.ParentTicketId;
        TicketEndpoints.AddAudit(dbContext, httpContext, ticket, "ticket.parent.updated", "The parent ticket was updated.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Guid?> ProjectIdAsync(
        Guid ticketId,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken) =>
        await dbContext.Tickets.AsNoTracking()
            .Where(entity => entity.Id == ticketId)
            .Select(entity => (Guid?)entity.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);
}

public sealed record CreateTicketRelationshipRequest(Guid TargetTicketId, TicketRelationshipType Type);

public sealed record SetParentTicketRequest(Guid? ParentTicketId);

public sealed record TicketRelationshipResponse(
    Guid Id,
    Guid RelatedTicketId,
    string RelatedTicketKey,
    string RelatedTicketTitle,
    TicketRelationshipType Type,
    string Direction,
    DateTimeOffset CreatedAt);
