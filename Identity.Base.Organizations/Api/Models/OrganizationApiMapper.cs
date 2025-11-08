using System;
using System.Linq;
using Identity.Base.Abstractions.Pagination;
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

    public static PagedResult<OrganizationDto> ToOrganizationPagedResult(PagedResult<Organization> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var items = result.Items
            .Select(ToOrganizationDto)
            .ToList();

        return new PagedResult<OrganizationDto>(result.Page, result.PageSize, result.TotalCount, items);
    }

    public static OrganizationMembershipDto ToMembershipDto(OrganizationMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new OrganizationMembershipDto
        {
            OrganizationId = membership.OrganizationId,
            UserId = membership.UserId,
            TenantId = membership.TenantId,
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
            RoleIds = member.RoleIds,
            CreatedAtUtc = member.CreatedAtUtc,
            UpdatedAtUtc = member.UpdatedAtUtc,
            Email = member.Email,
            DisplayName = member.DisplayName
        };
    }

    public static PagedResult<OrganizationMembershipDto> ToMemberPagedResult(OrganizationMemberListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var items = result.Members
            .Select(ToMembershipDto)
            .ToList();

        return new PagedResult<OrganizationMembershipDto>(result.Page, result.PageSize, result.TotalCount, items);
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

    public static PagedResult<OrganizationRoleDto> ToOrganizationRolePagedResult(PagedResult<OrganizationRole> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var items = result.Items
            .Select(ToRoleDto)
            .ToList();

        return new PagedResult<OrganizationRoleDto>(result.Page, result.PageSize, result.TotalCount, items);
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

    public static PagedResult<OrganizationInvitationDto> ToInvitationPagedResult(PagedResult<OrganizationInvitationRecord> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var items = result.Items
            .Select(ToInvitationDto)
            .ToList();

        return new PagedResult<OrganizationInvitationDto>(result.Page, result.PageSize, result.TotalCount, items);
    }
}
