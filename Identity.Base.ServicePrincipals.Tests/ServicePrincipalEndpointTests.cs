using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Identity.Base.Admin.Authorization;
using Identity.Base.Logging;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.ServicePrincipals.Api;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Domain;
using Identity.Base.ServicePrincipals.Extensions;
using Identity.Base.ServicePrincipals.Options;
using Identity.Base.ServicePrincipals.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;

namespace Identity.Base.ServicePrincipals.Tests;

public sealed class ServicePrincipalEndpointTests
{
    [Fact]
    public async Task Update_ReturnsConflict_WhenSaveDetectsConcurrentUpdate()
    {
        var saveChangesInterceptor = new ToggleConcurrencyExceptionInterceptor();
        var auditLogger = Substitute.For<IAuditLogger>();
        var principalDatabaseName = $"endpoint-concurrency-{Guid.NewGuid():N}";
        var roleDatabaseName = $"endpoint-concurrency-roles-{Guid.NewGuid():N}";
        await using var app = BuildTestApplication(
            principalDatabaseName,
            roleDatabaseName,
            auditLogger,
            saveChangesInterceptor);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var principal = new ServicePrincipal("Automation", "automation");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();
        saveChangesInterceptor.Enabled = true;

        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.PutAsJsonAsync($"/admin/service-principals/{principal.Id:D}", new
        {
            displayName = "Updated automation",
            concurrencyStamp = principal.ConcurrencyStamp
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        ((int)response.StatusCode).ShouldBe(StatusCodes.Status409Conflict, responseBody);
        responseBody.ShouldContain("Service principal was modified by another process.");
        await auditLogger.DidNotReceiveWithAnyArgs().LogAnonymousAsync(default!, default!, default);
    }

    [Fact]
    public async Task Update_ReturnsCurrentRoles()
    {
        var auditLogger = Substitute.For<IAuditLogger>();
        await using var app = BuildTestApplication(
            $"endpoint-update-{Guid.NewGuid():N}",
            $"endpoint-update-roles-{Guid.NewGuid():N}",
            auditLogger);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var roleDbContext = scope.ServiceProvider.GetRequiredService<IdentityRolesDbContext>();
        var principal = new ServicePrincipal("Automation", "automation");
        var role = new Role { Name = "Deployer" };
        dbContext.ServicePrincipals.Add(principal);
        roleDbContext.Roles.Add(role);
        roleDbContext.ServicePrincipalRoles.Add(new ServicePrincipalRole
        {
            ServicePrincipalId = principal.Id,
            Role = role
        });
        await dbContext.SaveChangesAsync();
        await roleDbContext.SaveChangesAsync();

        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.PutAsJsonAsync($"/admin/service-principals/{principal.Id:D}", new
        {
            displayName = "Updated automation",
            concurrencyStamp = principal.ConcurrencyStamp
        });

        var responseBody = await response.Content.ReadFromJsonAsync<ServicePrincipalSummary>();
        ((int)response.StatusCode).ShouldBe(StatusCodes.Status200OK);
        responseBody.ShouldNotBeNull();
        responseBody.Roles.ShouldBe(["Deployer"]);
    }

    [Fact]
    public async Task CreateAndUpdate_ReturnValidationForOversizedDisplayNames()
    {
        var auditLogger = Substitute.For<IAuditLogger>();
        await using var app = BuildTestApplication(
            $"endpoint-display-name-{Guid.NewGuid():N}",
            $"endpoint-display-name-roles-{Guid.NewGuid():N}",
            auditLogger);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var principal = new ServicePrincipal("Automation", "automation");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();

        await app.StartAsync();
        using var client = app.GetTestClient();
        var oversizedDisplayName = new string('d', ServicePrincipal.MaxDisplayNameLength + 1);
        var createResponse = await client.PostAsJsonAsync("/admin/service-principals", new
        {
            displayName = oversizedDisplayName
        });
        var updateResponse = await client.PutAsJsonAsync($"/admin/service-principals/{principal.Id:D}", new
        {
            displayName = oversizedDisplayName,
            concurrencyStamp = principal.ConcurrencyStamp
        });

        ((int)createResponse.StatusCode).ShouldBe(StatusCodes.Status400BadRequest);
        ((int)updateResponse.StatusCode).ShouldBe(StatusCodes.Status400BadRequest);
        (await createResponse.Content.ReadAsStringAsync()).ShouldContain("displayName");
        (await updateResponse.Content.ReadAsStringAsync()).ShouldContain("displayName");
        await auditLogger.DidNotReceiveWithAnyArgs().LogAnonymousAsync(default!, default!, default);
    }

    [Fact]
    public async Task PutRoles_ReturnsAndAuditsPersistedRoleNames()
    {
        var auditLogger = Substitute.For<IAuditLogger>();
        await using var app = BuildTestApplication(
            $"endpoint-put-roles-{Guid.NewGuid():N}",
            $"endpoint-put-roles-roles-{Guid.NewGuid():N}",
            auditLogger);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var roleDbContext = scope.ServiceProvider.GetRequiredService<IdentityRolesDbContext>();
        var principal = new ServicePrincipal("Automation", "automation");
        dbContext.ServicePrincipals.Add(principal);
        roleDbContext.Roles.AddRange(
            new Role { Name = "Deployer" },
            new Role { Name = "Reader" });
        await dbContext.SaveChangesAsync();
        await roleDbContext.SaveChangesAsync();

        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.PutAsJsonAsync($"/admin/service-principals/{principal.Id:D}/roles", new
        {
            roles = new[] { " Deployer ", "deployer", "Reader" }
        });

        var responseBody = await response.Content.ReadFromJsonAsync<ServicePrincipalRolesResponse>();
        ((int)response.StatusCode).ShouldBe(StatusCodes.Status200OK);
        responseBody.ShouldNotBeNull();
        responseBody.Roles.ShouldBe(["Deployer", "Reader"]);
        await auditLogger.Received(1).LogAnonymousAsync(
            AuditEventTypes.AdminServicePrincipalRolesUpdated,
            Arg.Is<object>(details => AuditRolesMatch(details, "Deployer", "Reader")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueCredential_AllowsNonExpiringCredential()
    {
        var auditLogger = Substitute.For<IAuditLogger>();
        await using var app = BuildTestApplication(
            $"endpoint-credential-{Guid.NewGuid():N}",
            $"endpoint-credential-roles-{Guid.NewGuid():N}",
            auditLogger);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var principal = new ServicePrincipal("Automation", "automation");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();

        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync($"/admin/service-principals/{principal.Id:D}/credentials", new
        {
            name = "primary",
            expiresAt = (DateTimeOffset?)null
        });

        var responseBody = await response.Content.ReadFromJsonAsync<IssuedServicePrincipalCredential>();
        ((int)response.StatusCode).ShouldBe(StatusCodes.Status201Created);
        responseBody.ShouldNotBeNull();
        responseBody.ExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task IssueCredential_ReturnsFieldSpecificValidationErrors()
    {
        var auditLogger = Substitute.For<IAuditLogger>();
        await using var app = BuildTestApplication(
            $"endpoint-invalid-credential-{Guid.NewGuid():N}",
            $"endpoint-invalid-credential-roles-{Guid.NewGuid():N}",
            auditLogger);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var principal = new ServicePrincipal("Automation", "automation");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();

        await app.StartAsync();
        using var client = app.GetTestClient();
        var nameResponse = await client.PostAsJsonAsync(
            $"/admin/service-principals/{principal.Id:D}/credentials",
            new { name = " ", expiresAt = (DateTimeOffset?)null });
        var expiryResponse = await client.PostAsJsonAsync(
            $"/admin/service-principals/{principal.Id:D}/credentials",
            new { name = "expired", expiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });

        ((int)nameResponse.StatusCode).ShouldBe(StatusCodes.Status400BadRequest);
        ((int)expiryResponse.StatusCode).ShouldBe(StatusCodes.Status400BadRequest);
        (await nameResponse.Content.ReadAsStringAsync()).ShouldContain("\"name\"");
        (await expiryResponse.Content.ReadAsStringAsync()).ShouldContain("\"expiresAt\"");
    }

    [Fact]
    public async Task IssueCredential_ReturnsConflictStatusInProblemDetails()
    {
        var auditLogger = Substitute.For<IAuditLogger>();
        await using var app = BuildTestApplication(
            $"endpoint-duplicate-credential-{Guid.NewGuid():N}",
            $"endpoint-duplicate-credential-roles-{Guid.NewGuid():N}",
            auditLogger);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ServicePrincipalService>();
        var principal = new ServicePrincipal("Automation", "automation");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();
        await service.IssueCredentialAsync(principal.Id, "primary", null, default);

        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync(
            $"/admin/service-principals/{principal.Id:D}/credentials",
            new { name = "primary", expiresAt = (DateTimeOffset?)null });

        var responseBody = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        ((int)response.StatusCode).ShouldBe(StatusCodes.Status409Conflict);
        responseBody.ShouldNotBeNull();
        responseBody.Status.ShouldBe(StatusCodes.Status409Conflict);
        responseBody.Detail.ShouldBe("Credential name already exists.");
        await auditLogger.DidNotReceiveWithAnyArgs().LogAnonymousAsync(default!, default!, default);
    }

    [Fact]
    public async Task RevokeCredential_ReturnsValidationForOversizedReason()
    {
        var auditLogger = Substitute.For<IAuditLogger>();
        await using var app = BuildTestApplication(
            $"endpoint-revoke-reason-{Guid.NewGuid():N}",
            $"endpoint-revoke-reason-roles-{Guid.NewGuid():N}",
            auditLogger);

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServicePrincipalDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ServicePrincipalService>();
        var principal = new ServicePrincipal("Automation", "automation");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();
        var issued = await service.IssueCredentialAsync(principal.Id, "primary", null, default);

        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync(
            $"/admin/service-principals/{principal.Id:D}/credentials/{issued.Credential.Id:D}/revoke",
            new { reason = new string('r', ServicePrincipalCredential.MaxRevokedReasonLength + 1) });

        ((int)response.StatusCode).ShouldBe(StatusCodes.Status400BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"reason\"");
        issued.Credential.RevokedAt.ShouldBeNull();
        await auditLogger.DidNotReceiveWithAnyArgs().LogAnonymousAsync(default!, default!, default);
    }

    [Fact]
    public void Endpoints_AreOptInAndCarryDedicatedAdminPermissions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddDbContext<ServicePrincipalDbContext>(options => options.UseInMemoryDatabase("endpoint-sp"));
        builder.Services.AddDbContext<IdentityRolesDbContext>(options => options.UseInMemoryDatabase("endpoint-roles"));
        builder.Services.AddScoped<IRoleDbContext>(provider => provider.GetRequiredService<IdentityRolesDbContext>());
        builder.Services.AddScoped<ServicePrincipalService>();
        builder.Services.AddSingleton(Substitute.For<IAuditLogger>());
        var app = builder.Build();
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToList()
            .Any(endpoint => endpoint.DisplayName?.Contains("service-principals", StringComparison.Ordinal) == true)
            .ShouldBeFalse();

        app.MapIdentityBaseServicePrincipalEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Where(endpoint => endpoint.DisplayName?.Contains("/admin/service-principals") == true)
            .ToList();
        endpoints.Count.ShouldBe(12);
        var permissions = endpoints.SelectMany(endpoint =>
                endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>()
                    .SelectMany(_ => endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>())
                    .SelectMany(policy => policy.Requirements.OfType<PermissionRequirement>())
                    .Select(requirement => requirement.Permission))
            .ToHashSet(StringComparer.Ordinal);
        permissions.ShouldBeSubsetOf(ServicePrincipalPermissions.All);
        permissions.ShouldContain(ServicePrincipalPermissions.ManageCredentials);
        permissions.ShouldContain(ServicePrincipalPermissions.Disable);
    }

    private static WebApplication BuildTestApplication(
        string principalDatabaseName,
        string roleDatabaseName,
        IAuditLogger auditLogger,
        ISaveChangesInterceptor? saveChangesInterceptor = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication(TestAuthenticationHandler.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.AuthenticationScheme,
                _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationHandler, AllowPermissionHandler>();
        builder.Services.AddDbContext<ServicePrincipalDbContext>(options =>
        {
            options.UseInMemoryDatabase(principalDatabaseName);
            if (saveChangesInterceptor is not null)
            {
                options.AddInterceptors(saveChangesInterceptor);
            }
        });
        builder.Services.AddDbContext<IdentityRolesDbContext>(options =>
            options.UseInMemoryDatabase(roleDatabaseName));
        builder.Services.AddScoped<IRoleDbContext>(provider => provider.GetRequiredService<IdentityRolesDbContext>());
        builder.Services.AddScoped<ServicePrincipalService>();
        builder.Services.AddSingleton(auditLogger);
        builder.Services.AddSingleton(Substitute.For<IOpenIddictApplicationManager>());
        builder.Services.AddSingleton(Substitute.For<IOpenIddictTokenManager>());
        builder.Services.AddSingleton<IPasswordHasher<ServicePrincipalCredential>, PasswordHasher<ServicePrincipalCredential>>();
        builder.Services.AddOptions<ServicePrincipalOptions>();
        var app = builder.Build();
        app.MapIdentityBaseServicePrincipalEndpoints();
        return app;
    }

    private static bool AuditRolesMatch(object details, params string[] expectedRoles) =>
        details.GetType().GetProperty("Roles")?.GetValue(details) is IEnumerable<string> roles
        && roles.SequenceEqual(expectedRoles);

    private sealed class ToggleConcurrencyExceptionInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            Enabled
                ? throw new DbUpdateConcurrencyException("Concurrent update detected.")
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private sealed class AllowPermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthenticationScheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "service-principal-test")],
                AuthenticationScheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
