using System.Linq;
using Identity.Base.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Roles.Infrastructure;

internal sealed class IdentityRolesMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdentityRolesMigrationHostedService> _logger;

    public IdentityRolesMigrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<IdentityRolesMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<IdentityRolesDbContext>();
        if (dbContext is null)
        {
            return;
        }

        if (dbContext.Database.IsRelational())
        {
            try
            {
                var pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
                var pendingList = pending.ToList();
                if (pendingList.Count > 0)
                {
                    _logger.LogInformation(
                        "Applying {Count} pending role migrations: {Migrations}",
                        pendingList.Count,
                        string.Join(", ", pendingList));

                    await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogInformation("No pending role migrations detected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply role database migrations");
                throw;
            }
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
