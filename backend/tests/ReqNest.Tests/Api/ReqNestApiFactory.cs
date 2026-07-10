using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ReqNest.Infrastructure.Notifications;
using ReqNest.Infrastructure.Persistence;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;

namespace ReqNest.Tests.Api;

public sealed class ReqNestApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.4-alpine")
        .WithDatabase("reqnest_tests")
        .WithUsername("reqnest")
        .WithPassword("reqnest_tests_password")
        .Build();
    private readonly AzuriteContainer azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
        .WithInMemoryPersistence()
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReqNest"] = postgres.GetConnectionString(),
                ["Storage:ConnectionString"] = azurite.GetConnectionString(),
                ["Storage:DefaultContainer"] = "requirements",
                ["Database:MigrateOnStartup"] = "false",
                ["Notifications:RunDeadlineWorker"] = "false",
                ["Notifications:RunEmailWorker"] = "false",
                ["Reports:RunScheduleWorker"] = "false",
                ["Integrations:RunWebhookWorker"] = "false",
                ["Integrations:RunConnectionWorker"] = "false",
                ["Authentication:RateLimit:PermitLimit"] = "1000",
            });
        });
        builder.ConfigureServices(services =>
        {
            var deadlineWorker = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(TicketDeadlineWorker));
            if (deadlineWorker is not null)
            {
                services.Remove(deadlineWorker);
            }

            services.RemoveAll<DbContextOptions<ReqNestDbContext>>();
            services.AddDbContext<ReqNestDbContext>(options =>
                options
                    .UseNpgsql(
                        postgres.GetConnectionString(),
                        npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                    .UseSnakeCaseNamingConvention());
        });
    }

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), azurite.StartAsync());

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReqNestDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        Dispose();
        await Task.WhenAll(postgres.DisposeAsync().AsTask(), azurite.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ReqNestApiFactory>
{
    public const string Name = "API integration";
}
