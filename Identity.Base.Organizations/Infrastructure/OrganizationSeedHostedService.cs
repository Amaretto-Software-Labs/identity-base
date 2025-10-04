using System;
using Identity.Base.Organizations.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Base.Organizations.Infrastructure;

public sealed class OrganizationSeedHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public OrganizationSeedHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetService<OrganizationRoleSeeder>();
        if (seeder is null)
        {
            return;
        }

        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
