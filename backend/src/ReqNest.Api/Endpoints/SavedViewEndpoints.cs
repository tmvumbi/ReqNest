using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Views;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class SavedViewEndpoints
{
    public static IEndpointRouteBuilder MapSavedViewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/saved-views")
            .RequireAuthorization()
            .WithTags("Saved views");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{viewId:guid}", UpdateAsync);
        group.MapDelete("/{viewId:guid}", DeleteAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
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
        var accessibleProjectIds = authorization.ProjectRoles.Keys
            .Concat(authorization.ProjectPermissions.Keys)
            .ToArray();
        var allProjects = authorization.AllProjectRoles.Count > 0 || authorization.AllProjectPermissions.Count > 0;
        var views = await dbContext.SavedViews.AsNoTracking()
            .Where(entity =>
                entity.OwnerUserId == userId ||
                entity.IsPublished && entity.ProjectId != null &&
                (allProjects || accessibleProjectIds.Contains(entity.ProjectId.Value)))
            .OrderBy(entity => entity.Name)
            .Select(entity => new SavedViewResponse(
                entity.Id,
                entity.Name,
                entity.ProjectId,
                entity.FiltersJson,
                entity.SortJson,
                entity.ColumnsJson,
                entity.GroupBy,
                entity.IsPublished,
                entity.OwnerUserId))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<SavedViewResponse>>(views);
    }

    private static async Task<IResult> CreateAsync(
        SaveViewRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var validation = Validate(request, authorization);
        if (validation is not null)
        {
            return validation(httpContext);
        }

        var view = new SavedView
        {
            TenantId = authorization.TenantId,
            OwnerUserId = httpContext.User.UserId(),
            Name = request.Name.Trim(),
            ProjectId = request.ProjectId,
            FiltersJson = request.Filters.GetRawText(),
            SortJson = request.Sort.GetRawText(),
            ColumnsJson = request.Columns.GetRawText(),
            GroupBy = request.GroupBy,
            IsPublished = request.IsPublished,
            PublishedByUserId = request.IsPublished ? httpContext.User.UserId() : null,
        };
        dbContext.SavedViews.Add(view);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/saved-views/{view.Id}", ToResponse(view));
    }

    private static async Task<IResult> UpdateAsync(
        Guid viewId,
        SaveViewRequest request,
        HttpContext httpContext,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = httpContext.TenantAuthorization();
        if (authorization is null)
        {
            return ApiProblems.TenantRequired(httpContext);
        }

        var validation = Validate(request, authorization);
        if (validation is not null)
        {
            return validation(httpContext);
        }

        var userId = httpContext.User.UserId();
        var view = await dbContext.SavedViews.SingleOrDefaultAsync(entity => entity.Id == viewId, cancellationToken);
        if (view is null)
        {
            return ApiProblems.NotFound(httpContext, "Saved view");
        }

        if (view.OwnerUserId != userId &&
            !(view.IsPublished && view.ProjectId is not null && authorization.CanManageProject(view.ProjectId.Value)))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        view.Name = request.Name.Trim();
        view.ProjectId = request.ProjectId;
        view.FiltersJson = request.Filters.GetRawText();
        view.SortJson = request.Sort.GetRawText();
        view.ColumnsJson = request.Columns.GetRawText();
        view.GroupBy = request.GroupBy;
        view.IsPublished = request.IsPublished;
        view.PublishedByUserId = request.IsPublished ? userId : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(view));
    }

    private static async Task<IResult> DeleteAsync(
        Guid viewId,
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
        var view = await dbContext.SavedViews.SingleOrDefaultAsync(entity => entity.Id == viewId, cancellationToken);
        if (view is null)
        {
            return ApiProblems.NotFound(httpContext, "Saved view");
        }

        if (view.OwnerUserId != userId &&
            !(view.IsPublished && view.ProjectId is not null && authorization.CanManageProject(view.ProjectId.Value)))
        {
            return ApiProblems.Forbidden(httpContext);
        }

        dbContext.SavedViews.Remove(view);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static Func<HttpContext, IResult>? Validate(SaveViewRequest request, Core.Identity.TenantAuthorization authorization)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        {
            return context => ApiProblems.Validation(context, "A view name of up to 120 characters is required.");
        }

        if (request.ProjectId is not null && !authorization.CanAccessProject(request.ProjectId.Value))
        {
            return context => ApiProblems.NotFound(context, "Project");
        }

        if (request.IsPublished &&
            (request.ProjectId is null || !authorization.CanManageProject(request.ProjectId.Value)))
        {
            return context => ApiProblems.Forbidden(context);
        }

        if (request.Filters.ValueKind != JsonValueKind.Object || request.Sort.ValueKind != JsonValueKind.Object ||
            request.Columns.ValueKind != JsonValueKind.Array)
        {
            return context => ApiProblems.Validation(context, "Filters, sorting, and columns have invalid structures.");
        }

        return null;
    }

    private static SavedViewResponse ToResponse(SavedView entity) => new(
        entity.Id,
        entity.Name,
        entity.ProjectId,
        entity.FiltersJson,
        entity.SortJson,
        entity.ColumnsJson,
        entity.GroupBy,
        entity.IsPublished,
        entity.OwnerUserId);
}

public sealed record SaveViewRequest(
    string Name,
    Guid? ProjectId,
    JsonElement Filters,
    JsonElement Sort,
    JsonElement Columns,
    string? GroupBy,
    bool IsPublished = false);

public sealed record SavedViewResponse(
    Guid Id,
    string Name,
    Guid? ProjectId,
    string FiltersJson,
    string SortJson,
    string ColumnsJson,
    string? GroupBy,
    bool IsPublished,
    Guid OwnerUserId);
