using System;
using System.Linq;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Api.Models;

internal static class OrganisationApiMapper
{
    public static OrganisationDto ToOrganisationDto(Organisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);

        return new OrganisationDto
        {
            Id = organisation.Id,
            TenantId = organisation.TenantId,
            Slug = organisation.Slug,
            DisplayName = organisation.DisplayName,
            Status = organisation.Status,
            Metadata = organisation.Metadata.Values,
            CreatedAtUtc = organisation.CreatedAtUtc,
            UpdatedAtUtc = organisation.UpdatedAtUtc,
            ArchivedAtUtc = organisation.ArchivedAtUtc
        };
    }

    public static OrganisationMembershipDto ToMembershipDto(OrganisationMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new OrganisationMembershipDto
        {
            OrganisationId = membership.OrganisationId,
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

    public static OrganisationMembershipDto ToMembershipDto(OrganisationMemberListItem member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new OrganisationMembershipDto
        {
            OrganisationId = member.OrganisationId,
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

    public static OrganisationMemberListResponse ToMemberListResponse(OrganisationMemberListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new OrganisationMemberListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Members = result.Members.Select(ToMembershipDto).ToArray()
        };
    }

    public static OrganisationRoleDto ToRoleDto(OrganisationRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new OrganisationRoleDto
        {
            Id = role.Id,
            OrganisationId = role.OrganisationId,
            TenantId = role.TenantId,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            CreatedAtUtc = role.CreatedAtUtc,
            UpdatedAtUtc = role.UpdatedAtUtc
        };
    }

    public static OrganisationRolePermissionsResponse ToRolePermissionsResponse(OrganisationRolePermissionSet permissionSet)
    {
        ArgumentNullException.ThrowIfNull(permissionSet);

        return new OrganisationRolePermissionsResponse
        {
            Effective = permissionSet.Effective.ToArray(),
            Explicit = permissionSet.Explicit.ToArray()
        };
    }

    public static OrganisationInvitationDto ToInvitationDto(OrganisationInvitationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new OrganisationInvitationDto
        {
            Code = record.Code,
            OrganisationId = record.OrganisationId,
            OrganisationSlug = record.OrganisationSlug,
            OrganisationName = record.OrganisationName,
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
