namespace ReqNest.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("System");

        group
            .MapGet("/status", () => TypedResults.Ok(new SystemStatus(
                "ReqNest.Api",
                "ready",
                DateTimeOffset.UtcNow)))
            .WithName("GetSystemStatus")
            .WithSummary("Returns the API scaffold status.");

        return endpoints;
    }
}

public sealed record SystemStatus(string Service, string Status, DateTimeOffset Timestamp);
