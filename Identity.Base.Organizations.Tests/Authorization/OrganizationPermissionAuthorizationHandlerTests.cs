using System;
using System.Security.Claims;
using Shouldly;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Claims;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Tests.Authorization;

public class OrganizationPermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenPermissionClaimPresent()
    {
        var resolver = new StubOrganizationPermissionResolver();
        var handler = CreateHandler(resolver);

        var requirement = new OrganizationPermissionRequirement(AdminOrganizationPermissions.OrganizationsRead);
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(RoleClaimTypes.Permissions, $"{AdminOrganizationPermissions.OrganizationsRead} {AdminOrganizationPermissions.OrganizationMembersRead}"),
            new Claim("scope", "identity.admin identity.api")
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenPermissionMissing()
    {
        var resolver = new StubOrganizationPermissionResolver();
        var handler = CreateHandler(resolver);

        var requirement = new OrganizationPermissionRequirement(AdminOrganizationPermissions.OrganizationsManage);
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(RoleClaimTypes.Permissions, AdminOrganizationPermissions.OrganizationsRead),
            new Claim("scope", "identity.admin identity.api")
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenResolverProvidesPermission()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resolver = new StubOrganizationPermissionResolver
        {
            Permissions = [UserOrganizationPermissions.OrganizationMembersManage],
            OrganizationId = organizationId,
            UserId = userId
        };

        var handler = CreateHandler(resolver);
        var requirement = new OrganizationPermissionRequirement(UserOrganizationPermissions.OrganizationMembersManage);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(OrganizationClaimTypes.OrganizationId, organizationId.ToString()),
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_Fails_WhenUserScopedButNoMembership()
    {
        var resolver = new StubOrganizationPermissionResolver();
        var handler = CreateHandler(resolver);

        var requirement = new OrganizationPermissionRequirement(UserOrganizationPermissions.OrganizationMembersManage);
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(RoleClaimTypes.Permissions, AdminOrganizationPermissions.OrganizationsRead)
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    private static OrganizationPermissionAuthorizationHandler CreateHandler(IOrganizationPermissionResolver resolver, string? adminScope = "identity.admin")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationAuthorizationOptions
        {
            AdminRequiredScope = adminScope
        });
        return new OrganizationPermissionAuthorizationHandler(resolver, options);
    }
}

internal sealed class StubOrganizationPermissionResolver : IOrganizationPermissionResolver
{
    public Guid? OrganizationId { get; set; }

    public Guid? UserId { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (OrganizationId.HasValue && OrganizationId.Value != organizationId)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (UserId.HasValue && UserId.Value != userId)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return Task.FromResult(Permissions);
    }

    public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
        => GetPermissionsAsync(organizationId, userId, cancellationToken);
}
