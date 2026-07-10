using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReqNest.Core.Storage;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;

namespace ReqNest.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReqNest");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'ReqNest' is required.");
        }

        services.AddDbContext<ReqNestDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services
            .AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection(BlobStorageOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ConnectionString) || options.ServiceUri is not null,
                "Storage:ConnectionString or Storage:ServiceUri is required.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return new BlobServiceClient(options.ConnectionString);
            }

            return new BlobServiceClient(
                options.ServiceUri,
                new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions
                    {
                        ExcludeInteractiveBrowserCredential = true,
                    }));
        });

        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        services.AddHealthChecks().AddDbContextCheck<ReqNestDbContext>("postgresql");

        return services;
    }
}
