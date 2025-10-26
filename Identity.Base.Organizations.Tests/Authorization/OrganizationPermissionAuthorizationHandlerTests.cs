using System.Security.Claims;
using FluentAssertions;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Base.Organizations.Tests.Authorization;

public class OrganizationPermissionAuthorizationHandlerTests
{
    private readonly OrganizationPermissionAuthorizationHandler _handler = new();

    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenPermissionClaimPresent()
    {
        var requirement = new OrganizationPermissionRequirement("organizations.read");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(RoleClaimTypes.Permissions, "organizations.read organization.members.read")
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenPermissionMissing()
    {
        var requirement = new OrganizationPermissionRequirement("organizations.manage");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(RoleClaimTypes.Permissions, "organizations.read")
        }, authenticationType: "Test"));

        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
