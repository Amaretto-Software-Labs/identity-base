using System;
using System.Security.Claims;
using Shouldly;
using Identity.Base.Organisations.Authorization;
using Identity.Base.Organisations.Claims;
using Identity.Base.Organisations.Services;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Base.Organisations.Tests.Authorization;

public class OrganisationPermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenPermissionClaimPresent()
    {
        var resolver = new StubOrganisationPermissionResolver();
        var handler = new OrganisationPermissionAuthorizationHandler(resolver);

        var requirement = new OrganisationPermissionRequirement("organisations.read");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(RoleClaimTypes.Permissions, "organisations.read organisation.members.read")
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenPermissionMissing()
    {
        var resolver = new StubOrganisationPermissionResolver();
        var handler = new OrganisationPermissionAuthorizationHandler(resolver);

        var requirement = new OrganisationPermissionRequirement("organisations.manage");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(RoleClaimTypes.Permissions, "organisations.read")
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenResolverProvidesPermission()
    {
        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resolver = new StubOrganisationPermissionResolver
        {
            Permissions = ["organisation.members.manage"],
            OrganisationId = organisationId,
            UserId = userId
        };

        var handler = new OrganisationPermissionAuthorizationHandler(resolver);
        var requirement = new OrganisationPermissionRequirement("organisation.members.manage");

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(OrganisationClaimTypes.OrganisationId, organisationId.ToString()),
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }
}

internal sealed class StubOrganisationPermissionResolver : IOrganisationPermissionResolver
{
    public Guid? OrganisationId { get; set; }

    public Guid? UserId { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (OrganisationId.HasValue && OrganisationId.Value != organisationId)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (UserId.HasValue && UserId.Value != userId)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return Task.FromResult(Permissions);
    }

    public Task<IReadOnlyList<string>> GetOrganisationPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
        => GetPermissionsAsync(organisationId, userId, cancellationToken);
}
