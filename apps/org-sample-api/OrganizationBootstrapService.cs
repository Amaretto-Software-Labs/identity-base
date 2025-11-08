using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrgSampleApi;

public sealed class OrganizationBootstrapService
{
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationMembershipService _membershipService;
    private readonly IOrganizationRoleService _roleService;
    private readonly IOptions<OrganizationRoleOptions> _roleOptions;
    private readonly ILogger<OrganizationBootstrapService> _logger;

    public OrganizationBootstrapService(
        IOrganizationService organizationService,
        IOrganizationMembershipService membershipService,
        IOrganizationRoleService roleService,
        IOptions<OrganizationRoleOptions> roleOptions,
        ILogger<OrganizationBootstrapService> logger)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _roleOptions = roleOptions ?? throw new ArgumentNullException(nameof(roleOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureOrganizationOwnerAsync(ApplicationUser user, OrganizationBootstrapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            _logger.LogDebug("Skipping organization bootstrap for user {UserId} because the slug is empty.", user.Id);
            return;
        }

        var organization = await _organizationService.GetBySlugAsync(null, request.Slug, cancellationToken)
            ?? await _organizationService.CreateAsync(new OrganizationCreateRequest
            {
                Slug = request.Slug,
                DisplayName = string.IsNullOrWhiteSpace(request.Name) ? request.Slug : request.Name.Trim(),
                Metadata = CreateMetadata(request.Metadata)
            }, cancellationToken).ConfigureAwait(false);

        var membership = await _membershipService.GetMembershipAsync(organization.Id, user.Id, cancellationToken).ConfigureAwait(false);
        var ownerRole = await ResolveOwnerRoleAsync(cancellationToken).ConfigureAwait(false);

        if (ownerRole is null)
        {
            _logger.LogWarning(
                "Organization owner role is not available. Continuing bootstrap for user {UserId} without assigning owner role.",
                user.Id);
        }

        if (membership is null)
        {
            await AddMembershipAsync(user, organization, ownerRole, cancellationToken).ConfigureAwait(false);
            return;
        }

        await UpdateMembershipIfNeededAsync(membership, organization, user, ownerRole, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddMembershipAsync(
        ApplicationUser user,
        Organization organization,
        OrganizationRole? ownerRole,
        CancellationToken cancellationToken)
    {
        var roleIds = ownerRole is null
            ? Array.Empty<Guid>()
            : new[] { ownerRole.Id };

        await _membershipService.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            RoleIds = roleIds
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Added user {UserId} to organization {OrganizationId} with {RoleCount} roles.",
            user.Id,
            organization.Id,
            roleIds.Length);
    }

    private async Task UpdateMembershipIfNeededAsync(
        OrganizationMembership membership,
        Organization organization,
        ApplicationUser user,
        OrganizationRole? ownerRole,
        CancellationToken cancellationToken)
    {
        var existingRoles = membership.RoleAssignments
            .Select(assignment => assignment.RoleId)
            .Where(roleId => roleId != Guid.Empty)
            .ToHashSet();

        var desiredRoles = new HashSet<Guid>(existingRoles);
        if (ownerRole is not null)
        {
            desiredRoles.Add(ownerRole.Id);
        }

        var rolesChanged = !desiredRoles.SetEquals(existingRoles);

        if (!rolesChanged)
        {
            _logger.LogDebug("No membership updates required for user {UserId} in organization {OrganizationId}.", user.Id, organization.Id);
            return;
        }

        await _membershipService.UpdateMembershipAsync(new OrganizationMembershipUpdateRequest
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            RoleIds = desiredRoles.Count > 0 ? desiredRoles.ToArray() : Array.Empty<Guid>()
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated membership for user {UserId} in organization {OrganizationId}. Role count: {RoleCount}",
            user.Id,
            organization.Id,
            desiredRoles.Count);
    }

    private async Task<OrganizationRole?> ResolveOwnerRoleAsync(CancellationToken cancellationToken)
    {
        var ownerRoleName = _roleOptions.Value.OwnerRoleName;
        if (string.IsNullOrWhiteSpace(ownerRoleName))
        {
            return null;
        }

        var roles = await _roleService.ListAsync(null, null, cancellationToken).ConfigureAwait(false);
        return roles.FirstOrDefault(role => role.OrganizationId is null && role.Name.Equals(ownerRoleName, StringComparison.OrdinalIgnoreCase));
    }

    private static OrganizationMetadata CreateMetadata(IReadOnlyDictionary<string, string?> metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return OrganizationMetadata.Empty;
        }

        return new OrganizationMetadata(new Dictionary<string, string?>(metadata, StringComparer.OrdinalIgnoreCase));
    }
}
