using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Data;

internal sealed class MigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MigrationHostedService> _logger;

    public MigrationHostedService(IServiceProvider serviceProvider, ILogger<MigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Skip migrations for in-memory databases (used in tests)
        if (dbContext.Database.IsInMemory())
        {
            _logger.LogInformation("Skipping migrations for in-memory database");

            // Ensure database is created for in-memory provider
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        try
        {
            _logger.LogInformation("Applying pending database migrations...");

            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingMigrationsList = pendingMigrations.ToList();

            if (pendingMigrationsList.Count > 0)
            {
                _logger.LogInformation("Found {Count} pending migrations: {Migrations}",
                    pendingMigrationsList.Count,
                    string.Join(", ", pendingMigrationsList));

                await dbContext.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("Successfully applied {Count} database migrations", pendingMigrationsList.Count);
            }
            else
            {
                _logger.LogInformation("No pending migrations found, database is up to date");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying database migrations");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
