using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReqNest.Core.Configuration;
using ReqNest.Core.Storage;
using ReqNest.Core.Content;
using ReqNest.Core.Notifications;
using ReqNest.Core.Reports;
using ReqNest.Core.Tenancy;
using ReqNest.Core.Identity;
using ReqNest.Core.Integrations;
using ReqNest.Infrastructure.Identity;
using ReqNest.Infrastructure.Integrations;
using ReqNest.Infrastructure.Content;
using ReqNest.Infrastructure.Configuration;
using ReqNest.Infrastructure.Notifications;
using ReqNest.Infrastructure.Reports;
using ReqNest.Infrastructure.Persistence;
using ReqNest.Infrastructure.Storage;
using ReqNest.Infrastructure.Tenancy;

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

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<IAuthenticationService>(serviceProvider =>
            serviceProvider.GetRequiredService<AuthenticationService>());
        services.AddScoped<ISessionValidationService>(serviceProvider =>
            serviceProvider.GetRequiredService<AuthenticationService>());
        services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();
        services.AddScoped<IWebhookEventPublisher, WebhookEventPublisher>();
        services.AddSingleton<IRichContentSanitizer, RichContentSanitizer>();
        services.AddScoped<ISlaCalculator, SlaCalculator>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IEmailDeliveryProvider, DevelopmentEmailDeliveryProvider>();
        if (configuration.GetValue<bool>("Notifications:RunDeadlineWorker"))
        {
            services.AddHostedService<TicketDeadlineWorker>();
        }
        if (configuration.GetValue<bool>("Notifications:RunEmailWorker"))
        {
            services.AddHostedService<EmailOutboxWorker>();
        }
        services.AddSingleton<IReportPdfGenerator, SimpleReportPdfGenerator>();
        services.AddDbContext<ReqNestDbContext>(options =>
            options.UseNpgsql(
                    connectionString,
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseSnakeCaseNamingConvention());

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
            var clientOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2023_11_03);

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return new BlobServiceClient(options.ConnectionString, clientOptions);
            }

            return new BlobServiceClient(
                options.ServiceUri,
                new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions
                    {
                        ExcludeInteractiveBrowserCredential = true,
                    }),
                clientOptions);
        });

        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        services.AddHealthChecks()
            .AddDbContextCheck<ReqNestDbContext>("postgresql", tags: ["ready"])
            .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: ["ready"]);

        return services;
    }
}
