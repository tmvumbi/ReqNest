using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ReqNest.Core.Auditing;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Infrastructure.Persistence;

namespace ReqNest.Api.Endpoints;

public static class ApiTokenEndpoints
{
    public static IEndpointRouteBuilder MapApiTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/api-tokens")
            .RequireAuthorization()
            .WithTags("API tokens");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{tokenId:guid}/revoke", RevokeAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true)
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.Forbidden(context);
        }

        var tokens = await dbContext.ApiTokens.AsNoTracking()
            .OrderBy(entity => entity.Name)
            .Select(entity => ToResponse(entity))
            .ToArrayAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyCollection<ApiTokenResponse>>(tokens);
    }

    private static async Task<IResult> CreateAsync(
        CreateApiTokenRequest request,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true || context.User.HasClaim(claim => claim.Type == "reqnest:api-token"))
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.Forbidden(context);
        }

        var scopes = request.Scopes.Distinct(StringComparer.Ordinal).ToArray();
        var projectIds = request.ProjectIds.Distinct().ToArray();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160 ||
            scopes.Length == 0 || scopes.Any(scope => !AppPermission.All.Contains(scope)) ||
            request.ExpiresAt is not null && request.ExpiresAt <= DateTimeOffset.UtcNow ||
            await dbContext.Projects.CountAsync(entity => projectIds.Contains(entity.Id), cancellationToken) != projectIds.Length)
        {
            return ApiProblems.Validation(context, "A valid name, scope, project set, and future expiry are required.");
        }

        var rawToken = "rqn_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var entity = new ApiToken
        {
            TenantId = authorization.TenantId,
            CreatedByUserId = context.User.UserId(),
            Name = request.Name.Trim(),
            Prefix = rawToken[..12],
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
            Scopes = scopes,
            ProjectIds = projectIds,
            ExpiresAt = request.ExpiresAt,
        };
        dbContext.ApiTokens.Add(entity);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = authorization.TenantId,
            ActorUserId = context.User.UserId(),
            Action = "api_token.created",
            TargetType = nameof(ApiToken),
            TargetId = entity.Id.ToString(),
            Summary = "A scoped API token was created.",
            CorrelationId = context.TraceIdentifier,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/api-tokens/{entity.Id}", new ApiTokenCreatedResponse(ToResponse(entity), rawToken));
    }

    private static async Task<IResult> RevokeAsync(
        Guid tokenId,
        HttpContext context,
        ReqNestDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authorization = context.TenantAuthorization();
        if (authorization?.IsTenantAdministrator() != true || context.User.HasClaim(claim => claim.Type == "reqnest:api-token"))
        {
            return authorization is null ? ApiProblems.TenantRequired(context) : ApiProblems.Forbidden(context);
        }

        var token = await dbContext.ApiTokens.SingleOrDefaultAsync(entity => entity.Id == tokenId, cancellationToken);
        if (token is null)
        {
            return ApiProblems.NotFound(context, "API token");
        }

        token.RevokedAt ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static ApiTokenResponse ToResponse(ApiToken entity) => new(
        entity.Id,
        entity.Name,
        entity.Prefix,
        entity.Scopes,
        entity.ProjectIds,
        entity.ExpiresAt,
        entity.RevokedAt,
        entity.LastUsedAt,
        entity.CreatedAt);
}

public sealed record CreateApiTokenRequest(
    string Name,
    IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<Guid> ProjectIds,
    DateTimeOffset? ExpiresAt);

public sealed record ApiTokenResponse(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<Guid> ProjectIds,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt);

public sealed record ApiTokenCreatedResponse(ApiTokenResponse Token, string RawToken);
