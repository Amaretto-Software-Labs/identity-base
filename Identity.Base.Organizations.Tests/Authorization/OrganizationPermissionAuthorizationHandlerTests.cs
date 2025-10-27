using System.Security.Claims;
using FluentAssertions;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Domain;
using Identity.Base.Roles.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Base.Organizations.Tests.Authorization;

public class OrganizationPermissionAuthorizationHandlerTests
{
    private readonly OrganizationPermissionAuthorizationHandler _handler = new(new StubMembershipService());

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

internal sealed class StubMembershipService : IOrganizationMembershipService
{
    public Task<OrganizationMembership> AddMemberAsync(OrganizationMembershipRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<OrganizationMembership?>(null);

    public Task<IReadOnlyList<OrganizationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<OrganizationMemberListResult> GetMembersAsync(OrganizationMemberListRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(OrganizationMemberListResult.Empty(request.Page < 1 ? 1 : request.Page, request.PageSize < 1 ? 1 : request.PageSize));

    public Task<OrganizationMembership> UpdateMembershipAsync(OrganizationMembershipUpdateRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
