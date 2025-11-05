using System.Linq;
using System.Security.Claims;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Claims;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Infrastructure;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Identity.Base.Organizations.Tests.Infrastructure;

public class OrganizationContextFromHeaderMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsContext_WhenHeaderAndMembershipValid()
    {
        await using var dbContext = CreateContext();
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Slug = "acme",
            DisplayName = "Acme",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var userId = dbContext.OrganizationMemberships.Single().UserId;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new Claim(OrganizationClaimTypes.OrganizationMemberships, organization.Id.ToString("D"))
        }, "Test"));

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IOrganizationContextAccessor, OrganizationContextAccessor>()
            .BuildServiceProvider();

        var accessor = serviceProvider.GetRequiredService<IOrganizationContextAccessor>();

        var httpContext = new DefaultHttpContext
        {
            User = principal,
            RequestServices = serviceProvider
        };
        httpContext.Request.Headers[OrganizationContextHeaderNames.OrganizationId] = organization.Id.ToString("D");
        httpContext.Request.Path = "/users/me/organizations/active";

        Guid? capturedOrganizationId = null;
        var middleware = new OrganizationContextFromHeaderMiddleware(_ =>
        {
            capturedOrganizationId = accessor.Current.OrganizationId;
            return Task.CompletedTask;
        }, OrganizationContextHeaderNames.OrganizationId);

        await middleware.InvokeAsync(httpContext, accessor, dbContext);

        capturedOrganizationId.ShouldBe(organization.Id);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsForbidden_WhenHeaderOrgNotInClaim()
    {
        await using var dbContext = CreateContext();
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Slug = "acme",
            DisplayName = "Acme",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new Claim(OrganizationClaimTypes.OrganizationMemberships, Guid.NewGuid().ToString("D"))
        }, "Test"));

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContext.Request.Headers[OrganizationContextHeaderNames.OrganizationId] = organization.Id.ToString("D");
        httpContext.Request.Path = "/users/me/organizations/active";

        var accessor = new OrganizationContextAccessor();
        var middleware = new OrganizationContextFromHeaderMiddleware(_ => Task.CompletedTask, OrganizationContextHeaderNames.OrganizationId);

        await middleware.InvokeAsync(httpContext, accessor, dbContext);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_AllowsAdmin_WhenNotMember()
    {
        await using var dbContext = CreateContext();
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Slug = "acme",
            DisplayName = "Acme",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new Claim(RoleClaimTypes.Permissions, AdminOrganizationPermissions.OrganizationsManage)
        }, "Test"));

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContext.Request.Headers[OrganizationContextHeaderNames.OrganizationId] = organization.Id.ToString("D");
        httpContext.Request.Path = "/users/me/organizations/active";

        var accessor = new OrganizationContextAccessor();
        Guid? capturedOrganizationId = null;

        var middleware = new OrganizationContextFromHeaderMiddleware(_ =>
        {
            capturedOrganizationId = accessor.Current.OrganizationId;
            return Task.CompletedTask;
        }, OrganizationContextHeaderNames.OrganizationId);

        await middleware.InvokeAsync(httpContext, accessor, dbContext);

        capturedOrganizationId.ShouldBe(organization.Id);
        httpContext.Response.StatusCode.ShouldNotBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_IgnoresHeader_ForAdminEndpoints()
    {
        await using var dbContext = CreateContext();
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Slug = "acme",
            DisplayName = "Acme",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new Claim(RoleClaimTypes.Permissions, AdminOrganizationPermissions.OrganizationsManage)
        }, "Test"));

        var accessor = new OrganizationContextAccessor();
        bool nextInvoked = false;

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContext.Request.Headers[OrganizationContextHeaderNames.OrganizationId] = organization.Id.ToString("D");
        httpContext.Request.Path = "/organizations";

        var middleware = new OrganizationContextFromHeaderMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, OrganizationContextHeaderNames.OrganizationId);

        await middleware.InvokeAsync(httpContext, accessor, dbContext);

        nextInvoked.ShouldBeTrue();
        accessor.Current.HasOrganization.ShouldBeFalse();
    }

    private static OrganizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"org-header-{Guid.NewGuid()}")
            .Options;
        return new OrganizationDbContext(options);
    }
}
