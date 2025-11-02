using System;
using Identity.Base.Organisations.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Base.Organisations.Infrastructure;

public sealed class OrganisationSeedHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public OrganisationSeedHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetService<OrganisationRoleSeeder>();
        if (seeder is null)
        {
            return;
        }

        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
