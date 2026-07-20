using Identity.Base.Admin.Authorization;
using Identity.Base.Logging;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Extensions;
using Identity.Base.ServicePrincipals.Options;
using Identity.Base.ServicePrincipals.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Identity.Base.ServicePrincipals.Tests;

public sealed class ServicePrincipalEndpointTests
{
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
}
