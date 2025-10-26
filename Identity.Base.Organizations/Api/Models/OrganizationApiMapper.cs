using System;
using System.Linq;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Api.Models;

internal static class OrganizationApiMapper
{
    public static OrganizationDto ToOrganizationDto(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new OrganizationDto
        {
            Id = organization.Id,
            TenantId = organization.TenantId,
            Slug = organization.Slug,
            DisplayName = organization.DisplayName,
            Status = organization.Status,
            Metadata = organization.Metadata.Values,
            CreatedAtUtc = organization.CreatedAtUtc,
            UpdatedAtUtc = organization.UpdatedAtUtc,
            ArchivedAtUtc = organization.ArchivedAtUtc
        };
    }

    public static OrganizationMembershipDto ToMembershipDto(OrganizationMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new OrganizationMembershipDto
        {
            OrganizationId = membership.OrganizationId,
            UserId = membership.UserId,
            TenantId = membership.TenantId,
            IsPrimary = membership.IsPrimary,
            RoleIds = membership.RoleAssignments.Select(assignment => assignment.RoleId).ToArray(),
            CreatedAtUtc = membership.CreatedAtUtc,
            UpdatedAtUtc = membership.UpdatedAtUtc
        };
    }

    public static OrganizationRoleDto ToRoleDto(OrganizationRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new OrganizationRoleDto
        {
            Id = role.Id,
            OrganizationId = role.OrganizationId,
            TenantId = role.TenantId,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            CreatedAtUtc = role.CreatedAtUtc,
            UpdatedAtUtc = role.UpdatedAtUtc
        };
    }
}
