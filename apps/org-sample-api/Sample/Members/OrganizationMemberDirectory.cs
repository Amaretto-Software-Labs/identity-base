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
        var members = await LoadAllMembersAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (members.Count == 0)
        {
            return Array.Empty<OrganizationMemberDetail>();
        }

        var userLookup = await LoadUserLookupAsync(members, cancellationToken).ConfigureAwait(false);

        return members
            .Select(member => Map(member, userLookup))
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

        var memberItem = new OrganizationMemberListItem
        {
            OrganizationId = membership.OrganizationId,
            UserId = membership.UserId,
            TenantId = membership.TenantId,
            RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
            CreatedAtUtc = membership.CreatedAtUtc,
            UpdatedAtUtc = membership.UpdatedAtUtc,
            Email = userInfo?.Email,
            DisplayName = userInfo?.DisplayName
        };

        return Map(memberItem, userInfo);
    }

    private async Task<IReadOnlyList<OrganizationMemberListItem>> LoadAllMembersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var result = new List<OrganizationMemberListItem>();
        var page = 1;
        OrganizationMemberListResult? current;

        do
        {
            current = await _membershipService.GetMembersAsync(new OrganizationMemberListRequest
            {
                OrganizationId = organizationId,
                Page = page,
                PageSize = 200
            }, cancellationToken).ConfigureAwait(false);

            if (current.Members.Count == 0)
            {
                break;
            }

            result.AddRange(current.Members);

            if (current.Page * current.PageSize >= current.TotalCount)
            {
                break;
            }

            page++;
        }
        while (true);

        return result;
    }

    private async Task<Dictionary<Guid, UserProjection>> LoadUserLookupAsync(IEnumerable<OrganizationMemberListItem> memberships, CancellationToken cancellationToken)
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

    private static OrganizationMemberDetail Map(OrganizationMemberListItem membership, Dictionary<Guid, UserProjection> userLookup)
    {
        userLookup.TryGetValue(membership.UserId, out var user);
        return Map(membership, user);
    }

    private static OrganizationMemberDetail Map(OrganizationMemberListItem membership, UserProjection? user)
    {
        return new OrganizationMemberDetail
        {
            OrganizationId = membership.OrganizationId,
            UserId = membership.UserId,
            RoleIds = membership.RoleIds.ToArray(),
            CreatedAtUtc = membership.CreatedAtUtc,
            UpdatedAtUtc = membership.UpdatedAtUtc,
            Email = user?.Email,
            DisplayName = user?.DisplayName
        };
    }

    private sealed record UserProjection(Guid Id, string? Email, string? DisplayName);
}
