using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationMembershipService
{
    Task<OrganizationMembership> AddMemberAsync(OrganizationMembershipRequest request, CancellationToken cancellationToken = default);

    Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default);

    Task<PagedResult<UserOrganizationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, PageRequest pageRequest, bool includeArchived, CancellationToken cancellationToken = default);

    Task<OrganizationMemberListResult> GetMembersAsync(OrganizationMemberListRequest request, CancellationToken cancellationToken = default);

    Task<OrganizationMembership> UpdateMembershipAsync(OrganizationMembershipUpdateRequest request, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}
