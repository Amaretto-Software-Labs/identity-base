using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace Identity.Base.Tests;

public class OpenIddictSeedingTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public OpenIddictSeedingTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenIddictSeeder_SeedsConfiguredApplications()
    {
        using var scope = _factory.Services.CreateScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var application = await applicationManager.FindByClientIdAsync("test-client");
        application.Should().NotBeNull();
    }

    [Fact]
    public async Task OpenIddictSeeder_SeedsConfiguredScopes()
    {
        using var scope = _factory.Services.CreateScope();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        var scopeEntity = await scopeManager.FindByNameAsync("identity.api");
        scopeEntity.Should().NotBeNull();
    }
}
