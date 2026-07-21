using Identity.Base.OpenIddict;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Domain;
using Identity.Base.ServicePrincipals.Extensions;
using Identity.Base.ServicePrincipals.Options;
using Identity.Base.ServicePrincipals.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;

namespace Identity.Base.ServicePrincipals.Tests;

public sealed class ServicePrincipalServiceTests
{
    [Fact]
    public async Task Create_RollsBackOpenIddictApplication_WhenPrincipalSaveFails()
    {
        var saveException = new DbUpdateException("Service principal save failed.");
        await using var fixture = new Fixture(new ThrowingSaveChangesInterceptor(saveException));
        var application = new object();
        fixture.ApplicationManager
            .CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(application));

        var exception = await Should.ThrowAsync<DbUpdateException>(() =>
            fixture.Service.CreateAsync("Automation", "automation", default));

        exception.ShouldBeSameAs(saveException);
        await fixture.ApplicationManager.Received(1).DeleteAsync(application, CancellationToken.None);
        fixture.Principals.ChangeTracker.Entries<ServicePrincipal>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_GeneratesUniqueKebabCaseClientIds()
    {
        await using var fixture = new Fixture();

        var first = await fixture.Service.CreateAsync("Claims Automation", default);
        var second = await fixture.Service.CreateAsync("Claims Automation", default);

        first.ClientId.ShouldStartWith("claims-automation-");
        second.ClientId.ShouldStartWith("claims-automation-");
        second.ClientId.ShouldNotBe(first.ClientId);
    }

    [Fact]
    public async Task MultipleCredentials_AreHashed_AndCanBeSelectivelyRevoked()
    {
        await using var fixture = new Fixture();
        var principal = await fixture.AddPrincipalAsync();
        var first = await fixture.Service.IssueCredentialAsync(principal.Id, "primary", null, default);
        var second = await fixture.Service.IssueCredentialAsync(principal.Id, "rollover", null, default);

        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, first.Secret, default)).ShouldBeTrue();
        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, second.Secret, default)).ShouldBeTrue();
        var stored = await fixture.Principals.ServicePrincipalCredentials.AsNoTracking().ToListAsync();
        stored.ShouldAllBe(item => item.SecretHash != first.Secret && item.SecretHash != second.Secret);

        await fixture.Service.RevokeCredentialAsync(principal.Id, first.Credential.Id, "rotation", default);

        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, first.Secret, default)).ShouldBeFalse();
        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, second.Secret, default)).ShouldBeTrue();
    }

    [Fact]
    public async Task Disable_RevokesCredentialsAndIssuedTokens()
    {
        await using var fixture = new Fixture();
        var principal = await fixture.AddPrincipalAsync();
        var issued = await fixture.Service.IssueCredentialAsync(principal.Id, "primary", null, default);

        await fixture.Service.DisableAsync(principal.Id, "disabled", default);

        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, issued.Secret, default)).ShouldBeFalse();
        fixture.TokenManager.Received(1)
            .FindBySubjectAsync(principal.Id.ToString("D"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManagedPrincipal_UsesGuidSubjectPrincipalTypeAndRolePermissions()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var suffix = Guid.NewGuid().ToString("N");
        services.AddDbContext<ServicePrincipalDbContext>(options => options.UseInMemoryDatabase($"sp-{suffix}"));
        services.AddDbContext<IdentityRolesDbContext>(options => options.UseInMemoryDatabase($"roles-{suffix}"));
        services.AddScoped<IRoleDbContext>(provider => provider.GetRequiredService<IdentityRolesDbContext>());
        services.AddIdentityBaseServicePrincipals(configuration);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var principals = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var roles = scope.ServiceProvider.GetRequiredService<IdentityRolesDbContext>();
        var principal = new ServicePrincipal("Build agent", "build-agent");
        var permission = new Permission { Name = "deployments.run" };
        var role = new Role { Name = "deployer", RolePermissions = [new RolePermission { Permission = permission }] };
        principals.ServicePrincipals.Add(principal);
        roles.Roles.Add(role);
        roles.ServicePrincipalRoles.Add(new ServicePrincipalRole { ServicePrincipalId = principal.Id, Role = role });
        await principals.SaveChangesAsync();
        await roles.SaveChangesAsync();

        var principalProvider = scope.ServiceProvider.GetServices<IClientCredentialsPrincipalProvider>().Single();
        var claimsPrincipal = await principalProvider.CreatePrincipalAsync(principal.ClientId, ["identity.api"]);

        claimsPrincipal.ShouldNotBeNull();
        claimsPrincipal.GetClaim(OpenIddictConstants.Claims.Subject).ShouldBe(principal.Id.ToString("D"));
        claimsPrincipal.GetClaim("identity.principal_type").ShouldBe("ServicePrincipal");
        claimsPrincipal.FindAll("identity.permissions").Select(claim => claim.Value).ShouldContain("deployments.run");
        claimsPrincipal.GetAccessTokenLifetime().ShouldBe(TimeSpan.FromMinutes(15));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(ISaveChangesInterceptor? saveChangesInterceptor = null)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var principalOptions = new DbContextOptionsBuilder<ServicePrincipalDbContext>()
                .UseInMemoryDatabase($"sp-{suffix}");
            if (saveChangesInterceptor is not null)
            {
                principalOptions.AddInterceptors(saveChangesInterceptor);
            }
            Principals = new ServicePrincipalDbContext(principalOptions.Options);
            Roles = new IdentityRolesDbContext(
                new DbContextOptionsBuilder<IdentityRolesDbContext>().UseInMemoryDatabase($"roles-{suffix}").Options);
            ApplicationManager = Substitute.For<IOpenIddictApplicationManager>();
            TokenManager = Substitute.For<IOpenIddictTokenManager>();
            TokenManager.FindBySubjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(EmptyTokens());
            Service = new ServicePrincipalService(
                Principals,
                Roles,
                ApplicationManager,
                TokenManager,
                new PasswordHasher<ServicePrincipalCredential>(),
                Microsoft.Extensions.Options.Options.Create(new ServicePrincipalOptions()),
                []);
        }

        public ServicePrincipalDbContext Principals { get; }
        public IdentityRolesDbContext Roles { get; }
        public IOpenIddictApplicationManager ApplicationManager { get; }
        public IOpenIddictTokenManager TokenManager { get; }
        public ServicePrincipalService Service { get; }

        public async Task<ServicePrincipal> AddPrincipalAsync()
        {
            var principal = new ServicePrincipal("Automation", $"automation-{Guid.NewGuid():N}");
            Principals.ServicePrincipals.Add(principal);
            await Principals.SaveChangesAsync();
            return principal;
        }

        public async ValueTask DisposeAsync()
        {
            await Principals.DisposeAsync();
            await Roles.DisposeAsync();
        }

        private static async IAsyncEnumerable<object> EmptyTokens()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class ThrowingSaveChangesInterceptor(Exception exception) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result) => throw exception;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) => throw exception;
    }
}
