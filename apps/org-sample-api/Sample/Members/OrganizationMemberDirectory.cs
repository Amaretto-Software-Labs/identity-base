using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Data;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;

namespace OrgSampleApi.Sample.Members;

public sealed class OrganizationMemberDirectory
{
    private readonly IOrganizationMembershipService _membershipService;
    private readonly AppDbContext _appDbContext;

    public OrganizationMemberDirectory(
        IOrganizationMembershipService membershipService,
        AppDbContext appDbContext)
    {
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
    }

    public async Task<IReadOnlyList<OrganizationMemberDetail>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var memberships = await _membershipService.GetMembersAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (memberships.Count == 0)
        {
            return Array.Empty<OrganizationMemberDetail>();
        }

        var userLookup = await LoadUserLookupAsync(memberships, cancellationToken).ConfigureAwait(false);

        return memberships
            .Select(membership => Map(membership, userLookup))
            .ToList();
    }

    public async Task<OrganizationMemberDetail?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        var membership = await _membershipService.GetMembershipAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return null;
        }

        var userInfo = await _appDbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => new UserProjection(user.Id, user.Email, user.DisplayName))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Map(membership, userInfo);
    }

    private async Task<Dictionary<Guid, UserProjection>> LoadUserLookupAsync(IEnumerable<OrganizationMembership> memberships, CancellationToken cancellationToken)
    {
        var userIds = memberships
            .Select(membership => membership.UserId)
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
        {
            return new Dictionary<Guid, UserProjection>();
        }

        var users = await _appDbContext.Users
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new UserProjection(user.Id, user.Email, user.DisplayName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.ToDictionary(user => user.Id);
    }

    private static OrganizationMemberDetail Map(OrganizationMembership membership, Dictionary<Guid, UserProjection> userLookup)
    {
        userLookup.TryGetValue(membership.UserId, out var user);
        return Map(membership, user);
    }

    private static OrganizationMemberDetail Map(OrganizationMembership membership, UserProjection? user)
    {
        return new OrganizationMemberDetail
        {
            OrganizationId = membership.OrganizationId,
            UserId = membership.UserId,
            IsPrimary = membership.IsPrimary,
            RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
            CreatedAtUtc = membership.CreatedAtUtc,
            UpdatedAtUtc = membership.UpdatedAtUtc,
            Email = user?.Email,
            DisplayName = user?.DisplayName
        };
    }

    private sealed record UserProjection(Guid Id, string? Email, string? DisplayName);
}
