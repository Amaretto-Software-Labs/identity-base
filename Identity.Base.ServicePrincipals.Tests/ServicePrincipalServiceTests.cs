using Identity.Base.OpenIddict;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Claims;
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

        var first = await fixture.Service.CreateAsync("  Claims Automation  ", default);
        var second = await fixture.Service.CreateAsync("Claims Automation", default);

        first.DisplayName.ShouldBe("Claims Automation");
        first.ClientId.ShouldStartWith("claims-automation-");
        second.ClientId.ShouldStartWith("claims-automation-");
        second.ClientId.ShouldNotBe(first.ClientId);
    }

    [Fact]
    public async Task Create_RejectsOversizedValuesBeforeOpenIddictRegistration()
    {
        await using var fixture = new Fixture();

        var displayNameException = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.Service.CreateAsync(
                new string('d', ServicePrincipal.MaxDisplayNameLength + 1),
                default));
        var clientIdException = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.Service.CreateAsync(
                "Automation",
                new string('c', ServicePrincipal.MaxClientIdLength + 1),
                default));

        displayNameException.ParamName.ShouldBe("displayName");
        clientIdException.ParamName.ShouldBe("clientId");
        await fixture.ApplicationManager.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default);
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
    public async Task IssueCredential_HashesAndVerifiesUsingPersistedCredentialInstance()
    {
        await using var fixture = new Fixture(passwordHasher: new CredentialBoundPasswordHasher());
        var principal = await fixture.AddPrincipalAsync();

        var issued = await fixture.Service.IssueCredentialAsync(principal.Id, "primary", null, default);

        var stored = await fixture.Principals.ServicePrincipalCredentials.SingleAsync();
        stored.Id.ShouldBe(issued.Credential.Id);
        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, issued.Secret, default)).ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateCredential_RehashesCredentialWhenHasherRequestsUpgrade()
    {
        var passwordHasher = new UpgradingPasswordHasher();
        await using var fixture = new Fixture(passwordHasher: passwordHasher);
        var principal = await fixture.AddPrincipalAsync();
        var credential = new ServicePrincipalCredential(
            principal.Id,
            "primary",
            "legacy:secret",
            null);
        fixture.Principals.ServicePrincipalCredentials.Add(credential);
        await fixture.Principals.SaveChangesAsync();

        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, "secret", default)).ShouldBeTrue();

        fixture.Principals.ChangeTracker.Clear();
        var stored = await fixture.Principals.ServicePrincipalCredentials.AsNoTracking().SingleAsync();
        stored.SecretHash.ShouldBe($"current:{credential.Id:N}:secret");
        passwordHasher.RehashNeededResults.ShouldBe(1);

        (await fixture.Service.ValidateCredentialAsync(principal.ClientId, "secret", default)).ShouldBeTrue();
        passwordHasher.RehashNeededResults.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task IssueCredential_RejectsBlankName(string name)
    {
        await using var fixture = new Fixture();
        var principal = await fixture.AddPrincipalAsync();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.Service.IssueCredentialAsync(principal.Id, name, null, default));

        exception.ParamName.ShouldBe("name");
        (await fixture.Principals.ServicePrincipalCredentials.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task IssueCredential_RejectsExpiredCredential()
    {
        await using var fixture = new Fixture();
        var principal = await fixture.AddPrincipalAsync();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.Service.IssueCredentialAsync(
                principal.Id,
                "expired",
                DateTimeOffset.UtcNow.AddMinutes(-1),
                default));

        exception.ParamName.ShouldBe("expiresAt");
        (await fixture.Principals.ServicePrincipalCredentials.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task IssueCredential_RejectsOversizedName()
    {
        await using var fixture = new Fixture();
        var principal = await fixture.AddPrincipalAsync();

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.Service.IssueCredentialAsync(
                principal.Id,
                new string('n', ServicePrincipalCredential.MaxNameLength + 1),
                null,
                default));

        exception.ParamName.ShouldBe("name");
        (await fixture.Principals.ServicePrincipalCredentials.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ReplaceRoles_MatchesRoleNamesIgnoringCase()
    {
        await using var fixture = new Fixture();
        var principal = await fixture.AddPrincipalAsync();
        var role = new Role { Name = "Deployer" };
        fixture.Roles.Roles.Add(role);
        await fixture.Roles.SaveChangesAsync();

        await fixture.Service.ReplaceRolesAsync(principal.Id, ["deployer"], default);

        var assignment = await fixture.Roles.ServicePrincipalRoles.SingleAsync();
        assignment.RoleId.ShouldBe(role.Id);
    }

    [Fact]
    public async Task RevokeCredential_RejectsOversizedReason()
    {
        await using var fixture = new Fixture();
        var principal = await fixture.AddPrincipalAsync();
        var issued = await fixture.Service.IssueCredentialAsync(principal.Id, "primary", null, default);

        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            fixture.Service.RevokeCredentialAsync(
                principal.Id,
                issued.Credential.Id,
                new string('r', ServicePrincipalCredential.MaxRevokedReasonLength + 1),
                default));

        exception.ParamName.ShouldBe("reason");
        issued.Credential.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void Credential_RevokeRejectsOversizedReasonWithoutMutation()
    {
        var credential = new ServicePrincipalCredential(Guid.NewGuid(), "primary", "hash", null);

        var exception = Should.Throw<ArgumentException>(() =>
            credential.Revoke(new string('r', ServicePrincipalCredential.MaxRevokedReasonLength + 1)));

        exception.ParamName.ShouldBe("reason");
        credential.RevokedAt.ShouldBeNull();
        credential.RevokedReason.ShouldBeNull();
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
        var role = new Role
        {
            Name = "deployer",
            RolePermissions =
            [
                new RolePermission { Permission = new Permission { Name = " deployments.run " } },
                new RolePermission { Permission = new Permission { Name = "Artifacts.Read" } }
            ]
        };
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
        claimsPrincipal.FindAll(RoleClaimTypes.Permissions).Single().Value
            .ShouldBe("Artifacts.Read deployments.run");
        claimsPrincipal.GetAccessTokenLifetime().ShouldBe(TimeSpan.FromMinutes(15));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(
            ISaveChangesInterceptor? saveChangesInterceptor = null,
            IPasswordHasher<ServicePrincipalCredential>? passwordHasher = null)
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
                passwordHasher ?? new PasswordHasher<ServicePrincipalCredential>(),
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

    private sealed class CredentialBoundPasswordHasher : IPasswordHasher<ServicePrincipalCredential>
    {
        public string HashPassword(ServicePrincipalCredential user, string password) =>
            $"{user.Id:N}:{password}";

        public PasswordVerificationResult VerifyHashedPassword(
            ServicePrincipalCredential user,
            string hashedPassword,
            string providedPassword) =>
            string.Equals(
                hashedPassword,
                $"{user.Id:N}:{providedPassword}",
                StringComparison.Ordinal)
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
    }

    private sealed class UpgradingPasswordHasher : IPasswordHasher<ServicePrincipalCredential>
    {
        public int RehashNeededResults { get; private set; }

        public string HashPassword(ServicePrincipalCredential user, string password) =>
            $"current:{user.Id:N}:{password}";

        public PasswordVerificationResult VerifyHashedPassword(
            ServicePrincipalCredential user,
            string hashedPassword,
            string providedPassword)
        {
            if (string.Equals(hashedPassword, $"legacy:{providedPassword}", StringComparison.Ordinal))
            {
                RehashNeededResults++;
                return PasswordVerificationResult.SuccessRehashNeeded;
            }

            return string.Equals(
                hashedPassword,
                HashPassword(user, providedPassword),
                StringComparison.Ordinal)
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
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
