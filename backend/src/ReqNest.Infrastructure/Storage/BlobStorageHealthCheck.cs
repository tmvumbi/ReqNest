using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ReqNest.Infrastructure.Storage;

public sealed class BlobStorageHealthCheck(BlobServiceClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Blob storage is unavailable.", exception);
        }
    }
}
