using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrgSampleApi;

public sealed class OrganisationBootstrapService
{
    private readonly IOrganisationService _organisationService;
    private readonly IOrganisationMembershipService _membershipService;
    private readonly IOrganisationRoleService _roleService;
    private readonly IOptions<OrganisationRoleOptions> _roleOptions;
    private readonly ILogger<OrganisationBootstrapService> _logger;

    public OrganisationBootstrapService(
        IOrganisationService organisationService,
        IOrganisationMembershipService membershipService,
        IOrganisationRoleService roleService,
        IOptions<OrganisationRoleOptions> roleOptions,
        ILogger<OrganisationBootstrapService> logger)
    {
        _organisationService = organisationService ?? throw new ArgumentNullException(nameof(organisationService));
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _roleOptions = roleOptions ?? throw new ArgumentNullException(nameof(roleOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureOrganisationOwnerAsync(ApplicationUser user, OrganisationBootstrapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            _logger.LogDebug("Skipping organisation bootstrap for user {UserId} because the slug is empty.", user.Id);
            return;
        }

        var organisation = await _organisationService.GetBySlugAsync(null, request.Slug, cancellationToken)
            ?? await _organisationService.CreateAsync(new OrganisationCreateRequest
            {
                Slug = request.Slug,
                DisplayName = string.IsNullOrWhiteSpace(request.Name) ? request.Slug : request.Name.Trim(),
                Metadata = CreateMetadata(request.Metadata)
            }, cancellationToken).ConfigureAwait(false);

        var membership = await _membershipService.GetMembershipAsync(organisation.Id, user.Id, cancellationToken).ConfigureAwait(false);
        var ownerRole = await ResolveOwnerRoleAsync(cancellationToken).ConfigureAwait(false);

        if (ownerRole is null)
        {
            _logger.LogWarning(
                "Organisation owner role is not available. Continuing bootstrap for user {UserId} without assigning owner role.",
                user.Id);
        }

        if (membership is null)
        {
            await AddMembershipAsync(user, organisation, ownerRole, cancellationToken).ConfigureAwait(false);
            return;
        }

        await UpdateMembershipIfNeededAsync(membership, organisation, user, ownerRole, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddMembershipAsync(
        ApplicationUser user,
        Organisation organisation,
        OrganisationRole? ownerRole,
        CancellationToken cancellationToken)
    {
        var roleIds = ownerRole is null
            ? Array.Empty<Guid>()
            : new[] { ownerRole.Id };

        await _membershipService.AddMemberAsync(new OrganisationMembershipRequest
        {
            OrganisationId = organisation.Id,
            UserId = user.Id,
            IsPrimary = true,
            RoleIds = roleIds
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Added user {UserId} to organisation {OrganisationId} as primary member with {RoleCount} roles.",
            user.Id,
            organisation.Id,
            roleIds.Length);
    }

    private async Task UpdateMembershipIfNeededAsync(
        OrganisationMembership membership,
        Organisation organisation,
        ApplicationUser user,
        OrganisationRole? ownerRole,
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
        var shouldPromoteToPrimary = !membership.IsPrimary;

        if (!rolesChanged && !shouldPromoteToPrimary)
        {
            _logger.LogDebug("No membership updates required for user {UserId} in organisation {OrganisationId}.", user.Id, organisation.Id);
            return;
        }

        await _membershipService.UpdateMembershipAsync(new OrganisationMembershipUpdateRequest
        {
            OrganisationId = organisation.Id,
            UserId = user.Id,
            IsPrimary = shouldPromoteToPrimary ? true : null,
            RoleIds = desiredRoles.Count > 0 ? desiredRoles.ToArray() : Array.Empty<Guid>()
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated membership for user {UserId} in organisation {OrganisationId}. Primary set: {Primary}, Role count: {RoleCount}",
            user.Id,
            organisation.Id,
            shouldPromoteToPrimary || membership.IsPrimary,
            desiredRoles.Count);
    }

    private async Task<OrganisationRole?> ResolveOwnerRoleAsync(CancellationToken cancellationToken)
    {
        var ownerRoleName = _roleOptions.Value.OwnerRoleName;
        if (string.IsNullOrWhiteSpace(ownerRoleName))
        {
            return null;
        }

        var roles = await _roleService.ListAsync(null, null, cancellationToken).ConfigureAwait(false);
        return roles.FirstOrDefault(role => role.OrganisationId is null && role.Name.Equals(ownerRoleName, StringComparison.OrdinalIgnoreCase));
    }

    private static OrganisationMetadata CreateMetadata(IReadOnlyDictionary<string, string?> metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return OrganisationMetadata.Empty;
        }

        return new OrganisationMetadata(new Dictionary<string, string?>(metadata, StringComparer.OrdinalIgnoreCase));
    }
}
