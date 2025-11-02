using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Abstractions;

public interface IOrganisationMembershipService
{
    Task<OrganisationMembership> AddMemberAsync(OrganisationMembershipRequest request, CancellationToken cancellationToken = default);

    Task<OrganisationMembership?> GetMembershipAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganisationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default);

    Task<OrganisationMemberListResult> GetMembersAsync(OrganisationMemberListRequest request, CancellationToken cancellationToken = default);

    Task<OrganisationMembership> UpdateMembershipAsync(OrganisationMembershipUpdateRequest request, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default);
}
