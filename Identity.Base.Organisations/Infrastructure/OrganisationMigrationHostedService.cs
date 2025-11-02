using System.Linq;
using Identity.Base.Organisations.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Organisations.Infrastructure;

public sealed class OrganisationMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrganisationMigrationHostedService> _logger;

    public OrganisationMigrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<OrganisationMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<OrganisationDbContext>();
        if (dbContext is null)
        {
            return;
        }

        if (dbContext.Database.IsRelational())
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            var pendingList = pendingMigrations.ToList();
            if (pendingList.Count == 0)
            {
                _logger.LogInformation("No pending organisation migrations detected.");
                return;
            }

            _logger.LogInformation(
                "Applying {Count} pending organisation migrations: {Migrations}",
                pendingList.Count,
                string.Join(", ", pendingList));

            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
