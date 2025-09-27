using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Base.Roles.Infrastructure;

internal sealed class IdentityRolesSeedHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public IdentityRolesSeedHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetService<Services.IRoleSeeder>();
        if (seeder is null)
        {
            return;
        }

        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
