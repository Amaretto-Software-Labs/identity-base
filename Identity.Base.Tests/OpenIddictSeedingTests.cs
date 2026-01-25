using System.Threading.Tasks;
using Shouldly;
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
        application.ShouldNotBeNull();
    }

    [Fact]
    public async Task OpenIddictSeeder_SeedsConfiguredScopes()
    {
        using var scope = _factory.Services.CreateScope();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        var scopeEntity = await scopeManager.FindByNameAsync("identity.api");
        scopeEntity.ShouldNotBeNull();
    }

    [Fact]
    public async Task OpenIddictSeeder_DoesNotSeedPasswordGrantPermissions()
    {
        using var scope = _factory.Services.CreateScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var allowed = await applicationManager.FindByClientIdAsync("test-client");
        allowed.ShouldNotBeNull();
        var allowedPermissions = await applicationManager.GetPermissionsAsync(allowed!);
        allowedPermissions.ShouldNotContain(OpenIddictConstants.Permissions.GrantTypes.Password);

        var disallowed = await applicationManager.FindByClientIdAsync("spa-client");
        disallowed.ShouldNotBeNull();
        var disallowedPermissions = await applicationManager.GetPermissionsAsync(disallowed!);
        disallowedPermissions.ShouldNotContain(OpenIddictConstants.Permissions.GrantTypes.Password);
    }

    [Fact]
    public async Task OpenIddictSeeder_AddsClientCredentialsGrantOnlyForAllowedClients()
    {
        using var scope = _factory.Services.CreateScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var allowed = await applicationManager.FindByClientIdAsync("test-client");
        allowed.ShouldNotBeNull();
        var allowedPermissions = await applicationManager.GetPermissionsAsync(allowed!);
        allowedPermissions.ShouldContain(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

        var disallowed = await applicationManager.FindByClientIdAsync("spa-client");
        disallowed.ShouldNotBeNull();
        var disallowedPermissions = await applicationManager.GetPermissionsAsync(disallowed!);
        disallowedPermissions.ShouldNotContain(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
    }

    [Fact]
    public async Task OpenIddictSeeder_NormalizesScopePermissions()
    {
        using var scope = _factory.Services.CreateScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var application = await applicationManager.FindByClientIdAsync("scope-prefix-client");
        application.ShouldNotBeNull();

        var permissions = await applicationManager.GetPermissionsAsync(application!);
        permissions.ShouldContain(OpenIddictConstants.Permissions.Prefixes.Scope + "aurora.api");
        permissions.ShouldContain(OpenIddictConstants.Permissions.Prefixes.Scope + "legacy.api");
        permissions.ShouldNotContain("scope:aurora.api");
        permissions.ShouldNotContain("scopes:legacy.api");
        permissions.ShouldNotContain(OpenIddictConstants.Permissions.Endpoints.Authorization);
        permissions.ShouldNotContain(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId);
    }

    [Fact]
    public async Task OpenIddictSeeder_DoesNotAddImplicitRequirements()
    {
        using var scope = _factory.Services.CreateScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var application = await applicationManager.FindByClientIdAsync("scope-prefix-client");
        application.ShouldNotBeNull();

        var requirements = await applicationManager.GetRequirementsAsync(application!);
        requirements.ShouldNotContain(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
    }
}
