using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Identity.Base.Admin.Authorization;
using Identity.Base.Logging;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
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
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication(TestAuthenticationHandler.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.AuthenticationScheme,
                _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationHandler, AllowPermissionHandler>();
        builder.Services.AddDbContext<ServicePrincipalDbContext>(options => options
            .UseInMemoryDatabase(principalDatabaseName)
            .AddInterceptors(saveChangesInterceptor));
        builder.Services.AddDbContext<IdentityRolesDbContext>(options =>
            options.UseInMemoryDatabase(roleDatabaseName));
        builder.Services.AddScoped<IRoleDbContext>(provider => provider.GetRequiredService<IdentityRolesDbContext>());
        builder.Services.AddScoped<ServicePrincipalService>();
        builder.Services.AddSingleton(auditLogger);
        builder.Services.AddSingleton(Substitute.For<IOpenIddictApplicationManager>());
        builder.Services.AddSingleton(Substitute.For<IOpenIddictTokenManager>());
        builder.Services.AddSingleton<IPasswordHasher<ServicePrincipalCredential>, PasswordHasher<ServicePrincipalCredential>>();
        builder.Services.AddOptions<ServicePrincipalOptions>();
        await using var app = builder.Build();
        app.MapIdentityBaseServicePrincipalEndpoints();

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
