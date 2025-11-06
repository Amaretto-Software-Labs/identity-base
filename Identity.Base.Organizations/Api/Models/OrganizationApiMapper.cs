using System;
using System.Linq;
using Identity.Base.Organizations.Abstractions;
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
            UpdatedAtUtc = membership.UpdatedAtUtc,
            Email = null,
            DisplayName = null
        };
    }

    public static OrganizationMembershipDto ToMembershipDto(OrganizationMemberListItem member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new OrganizationMembershipDto
        {
            OrganizationId = member.OrganizationId,
            UserId = member.UserId,
            TenantId = member.TenantId,
            IsPrimary = member.IsPrimary,
            RoleIds = member.RoleIds,
            CreatedAtUtc = member.CreatedAtUtc,
            UpdatedAtUtc = member.UpdatedAtUtc,
            Email = member.Email,
            DisplayName = member.DisplayName
        };
    }

    public static OrganizationMemberListResponse ToMemberListResponse(OrganizationMemberListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new OrganizationMemberListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Members = result.Members.Select(ToMembershipDto).ToArray()
        };
    }

    public static UserOrganizationMembershipDto ToUserOrganizationMembershipDto(UserOrganizationMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new UserOrganizationMembershipDto(
            membership.OrganizationId,
            membership.TenantId,
            membership.Slug,
            membership.DisplayName,
            membership.Status,
            membership.IsPrimary,
            membership.RoleIds,
            membership.CreatedAtUtc,
            membership.UpdatedAtUtc);
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

    public static OrganizationRolePermissionsResponse ToRolePermissionsResponse(OrganizationRolePermissionSet permissionSet)
    {
        ArgumentNullException.ThrowIfNull(permissionSet);

        return new OrganizationRolePermissionsResponse
        {
            Effective = permissionSet.Effective.ToArray(),
            Explicit = permissionSet.Explicit.ToArray()
        };
    }

    public static OrganizationInvitationDto ToInvitationDto(OrganizationInvitationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new OrganizationInvitationDto
        {
            Code = record.Code,
            OrganizationId = record.OrganizationId,
            OrganizationSlug = record.OrganizationSlug,
            OrganizationName = record.OrganizationName,
            Email = record.Email,
            RoleIds = record.RoleIds,
            CreatedBy = record.CreatedBy,
            CreatedAtUtc = record.CreatedAtUtc,
            ExpiresAtUtc = record.ExpiresAtUtc,
            UsedAtUtc = record.UsedAtUtc,
            UsedByUserId = record.UsedByUserId
        };
    }
}
