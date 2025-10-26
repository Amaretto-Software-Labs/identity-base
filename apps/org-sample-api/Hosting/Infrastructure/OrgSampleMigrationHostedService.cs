using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrgSampleApi.Sample.Data;

namespace OrgSampleApi.Hosting.Infrastructure;

internal sealed class OrgSampleMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrgSampleMigrationHostedService> _logger;

    public OrgSampleMigrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<OrgSampleMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<OrgSampleDbContext>();
        if (dbContext is null)
        {
            return;
        }

        if (!dbContext.Database.IsRelational())
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            var pendingList = pending.ToList();
            if (pendingList.Count == 0)
            {
                _logger.LogInformation("No pending invitation migrations detected.");
                return;
            }

            _logger.LogInformation(
                "Applying {Count} invitation migrations: {Migrations}",
                pendingList.Count,
                string.Join(", ", pendingList));

            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully applied invitation migrations.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to apply invitation database migrations");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
