using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Identity;

public sealed class IdentitySeedHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdentitySeedHostedService> _logger;

    public IdentitySeedHostedService(IServiceProvider serviceProvider, ILogger<IdentitySeedHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();

        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding identity data");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
