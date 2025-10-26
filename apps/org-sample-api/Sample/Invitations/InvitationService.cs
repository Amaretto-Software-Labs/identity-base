using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;
using Microsoft.Extensions.Logging;

namespace OrgSampleApi.Sample.Invitations;

public sealed class InvitationService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan MinimumLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromDays(30);

    private readonly IInvitationStore _store;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationMembershipService _membershipService;
    private readonly IOrganizationRoleService _roleService;
    private readonly ILogger<InvitationService> _logger;

    public InvitationService(
        IInvitationStore store,
        IOrganizationService organizationService,
        IOrganizationMembershipService membershipService,
        IOrganizationRoleService roleService,
        ILogger<InvitationService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InvitationRecord> CreateAsync(
        Guid organizationId,
        string email,
        IReadOnlyCollection<Guid> roleIds,
        Guid? createdBy,
        int? expiresInHours,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var organization = await _organizationService.GetByIdAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            throw new KeyNotFoundException($"Organization {organizationId} was not found.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedRoles = NormalizeRoleIds(roleIds);
        await EnsureRolesExistAsync(normalizedRoles, organization, cancellationToken).ConfigureAwait(false);

        var lifetime = ResolveLifetime(expiresInHours);

        var record = new InvitationRecord
        {
            Code = Guid.NewGuid(),
            OrganizationId = organization.Id,
            OrganizationSlug = organization.Slug,
            OrganizationName = organization.DisplayName,
            Email = normalizedEmail,
            RoleIds = normalizedRoles.ToArray(),
            CreatedBy = createdBy,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(lifetime)
        };

        await _store.CreateAsync(record, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created invitation {InvitationCode} for organization {OrganizationId} ({Email}).",
            record.Code,
            organization.Id,
            normalizedEmail);

        return record;
    }

    public Task<IReadOnlyCollection<InvitationRecord>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
        => _store.ListAsync(organizationId, cancellationToken);

    public async Task<bool> RevokeAsync(Guid organizationId, Guid code, CancellationToken cancellationToken = default)
    {
        var invitation = await _store.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (invitation is null || invitation.OrganizationId != organizationId)
        {
            return false;
        }

        await _store.RemoveAsync(code, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Revoked invitation {InvitationCode} for organization {OrganizationId}.", code, organizationId);
        return true;
    }

    public Task<InvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
        => _store.FindAsync(code, cancellationToken);

    public async Task<InvitationAcceptanceResult?> AcceptAsync(Guid code, ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var invitation = await _store.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            return null;
        }

        if (!string.Equals(invitation.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invitation email does not match the signed-in user.");
        }

        var organization = await _organizationService.GetByIdAsync(invitation.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            await _store.RemoveAsync(code, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var membership = await _membershipService.GetMembershipAsync(invitation.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        var roleIds = invitation.RoleIds ?? Array.Empty<Guid>();

        if (membership is null)
        {
            await _membershipService.AddMemberAsync(new OrganizationMembershipRequest
            {
                OrganizationId = invitation.OrganizationId,
                UserId = user.Id,
                TenantId = organization.TenantId,
                IsPrimary = false,
                RoleIds = roleIds
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var mergedRoleIds = membership.RoleAssignments
                .Select(assignment => assignment.RoleId)
                .Where(id => id != Guid.Empty)
                .Union(roleIds)
                .Distinct()
                .ToArray();

            if (mergedRoleIds.Any())
            {
                await _membershipService.UpdateMembershipAsync(new OrganizationMembershipUpdateRequest
                {
                    OrganizationId = invitation.OrganizationId,
                    UserId = user.Id,
                    IsPrimary = null,
                    RoleIds = mergedRoleIds
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        await _store.RemoveAsync(code, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} accepted invitation {InvitationCode} for organization {OrganizationId}.",
            user.Id,
            code,
            invitation.OrganizationId);

        return new InvitationAcceptanceResult
        {
            OrganizationId = organization.Id,
            OrganizationSlug = organization.Slug,
            OrganizationName = organization.DisplayName,
            RoleIds = roleIds,
            WasExistingMember = membership is not null
        };
    }

    private static HashSet<Guid> NormalizeRoleIds(IReadOnlyCollection<Guid> roleIds)
    {
        var result = new HashSet<Guid>();
        if (roleIds is null)
        {
            return result;
        }

        foreach (var roleId in roleIds)
        {
            if (roleId != Guid.Empty)
            {
                result.Add(roleId);
            }
        }

        return result;
    }

    private static TimeSpan ResolveLifetime(int? expiresInHours)
    {
        if (!expiresInHours.HasValue)
        {
            return DefaultLifetime;
        }

        var lifetime = TimeSpan.FromHours(Math.Max(0, expiresInHours.Value));
        if (lifetime < MinimumLifetime)
        {
            lifetime = MinimumLifetime;
        }
        else if (lifetime > MaximumLifetime)
        {
            lifetime = MaximumLifetime;
        }

        return lifetime;
    }

    private async Task EnsureRolesExistAsync(HashSet<Guid> roleIds, Organization organization, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return;
        }

        var roles = await _roleService.ListAsync(organization.TenantId, organization.Id, cancellationToken).ConfigureAwait(false);
        var available = roles.ToDictionary(role => role.Id);

        foreach (var roleId in roleIds)
        {
            if (!available.TryGetValue(roleId, out var role))
            {
                throw new InvalidOperationException("One or more roles are not valid for this organization.");
            }

            if (role.OrganizationId.HasValue && role.OrganizationId != organization.Id)
            {
                throw new InvalidOperationException("One or more roles are not valid for this organization.");
            }
        }
    }
}

