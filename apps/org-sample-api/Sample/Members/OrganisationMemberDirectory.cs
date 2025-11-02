using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Data;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;

namespace OrgSampleApi.Sample.Members;

public sealed class OrganisationMemberDirectory
{
    private readonly IOrganisationMembershipService _membershipService;
    private readonly AppDbContext _appDbContext;

    public OrganisationMemberDirectory(
        IOrganisationMembershipService membershipService,
        AppDbContext appDbContext)
    {
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
    }

    public async Task<IReadOnlyList<OrganisationMemberDetail>> GetMembersAsync(Guid organisationId, CancellationToken cancellationToken = default)
    {
        var members = await LoadAllMembersAsync(organisationId, cancellationToken).ConfigureAwait(false);
        if (members.Count == 0)
        {
            return Array.Empty<OrganisationMemberDetail>();
        }

        var userLookup = await LoadUserLookupAsync(members, cancellationToken).ConfigureAwait(false);

        return members
            .Select(member => Map(member, userLookup))
            .ToList();
    }

    public async Task<OrganisationMemberDetail?> GetMemberAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        var membership = await _membershipService.GetMembershipAsync(organisationId, userId, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return null;
        }

        var userInfo = await _appDbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => new UserProjection(user.Id, user.Email, user.DisplayName))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var memberItem = new OrganisationMemberListItem
        {
            OrganisationId = membership.OrganisationId,
            UserId = membership.UserId,
            TenantId = membership.TenantId,
            IsPrimary = membership.IsPrimary,
            RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
            CreatedAtUtc = membership.CreatedAtUtc,
            UpdatedAtUtc = membership.UpdatedAtUtc,
            Email = userInfo?.Email,
            DisplayName = userInfo?.DisplayName
        };

        return Map(memberItem, userInfo);
    }

    private async Task<IReadOnlyList<OrganisationMemberListItem>> LoadAllMembersAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var result = new List<OrganisationMemberListItem>();
        var page = 1;
        OrganisationMemberListResult? current;

        do
        {
            current = await _membershipService.GetMembersAsync(new OrganisationMemberListRequest
            {
                OrganisationId = organisationId,
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

    private async Task<Dictionary<Guid, UserProjection>> LoadUserLookupAsync(IEnumerable<OrganisationMemberListItem> memberships, CancellationToken cancellationToken)
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

    private static OrganisationMemberDetail Map(OrganisationMemberListItem membership, Dictionary<Guid, UserProjection> userLookup)
    {
        userLookup.TryGetValue(membership.UserId, out var user);
        return Map(membership, user);
    }

    private static OrganisationMemberDetail Map(OrganisationMemberListItem membership, UserProjection? user)
    {
        return new OrganisationMemberDetail
        {
            OrganisationId = membership.OrganisationId,
            UserId = membership.UserId,
            IsPrimary = membership.IsPrimary,
            RoleIds = membership.RoleIds.ToArray(),
            CreatedAtUtc = membership.CreatedAtUtc,
            UpdatedAtUtc = membership.UpdatedAtUtc,
            Email = user?.Email,
            DisplayName = user?.DisplayName
        };
    }

    private sealed record UserProjection(Guid Id, string? Email, string? DisplayName);
}
