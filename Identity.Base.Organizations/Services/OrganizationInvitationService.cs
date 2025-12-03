using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;
using Microsoft.Extensions.Logging;
using Identity.Base.Organizations.Lifecycle;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationInvitationService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan MinimumLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromDays(30);

    private readonly IOrganizationInvitationStore _store;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationMembershipService _membershipService;
    private readonly IOrganizationRoleService _roleService;
    private readonly ILogger<OrganizationInvitationService> _logger;
    private readonly ILogSanitizer _logSanitizer;
    private readonly IOrganizationLifecycleHookDispatcher _lifecycleDispatcher;

    public OrganizationInvitationService(
        IOrganizationInvitationStore store,
        IOrganizationService organizationService,
        IOrganizationMembershipService membershipService,
        IOrganizationRoleService roleService,
        ILogger<OrganizationInvitationService> logger,
        ILogSanitizer logSanitizer,
        IOrganizationLifecycleHookDispatcher lifecycleDispatcher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logSanitizer = logSanitizer ?? throw new ArgumentNullException(nameof(logSanitizer));
        _lifecycleDispatcher = lifecycleDispatcher ?? throw new ArgumentNullException(nameof(lifecycleDispatcher));
    }

    public async Task<OrganizationInvitationRecord> CreateAsync(
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

        var hasActiveInvitation = await _store.HasActiveInvitationAsync(organization.Id, normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (hasActiveInvitation)
        {
            throw new OrganizationInvitationAlreadyExistsException(normalizedEmail);
        }

        var lifetime = ResolveLifetime(expiresInHours);

        var record = new OrganizationInvitationRecord
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

        var lifecycleContext = new OrganizationLifecycleContext(
            OrganizationLifecycleEvent.InvitationCreated,
            organization.Id,
            organization.Slug,
            organization.DisplayName,
            ActorUserId: createdBy,
            Organization: organization,
            Invitation: record);

        await _lifecycleDispatcher.EnsureCanCreateInvitationAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);

        await _store.CreateAsync(record, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created invitation {InvitationCode} for organization {OrganizationId} ({Email}).",
            record.Code,
            organization.Id,
            _logSanitizer.RedactEmail(normalizedEmail));

        await _lifecycleDispatcher.NotifyInvitationCreatedAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);

        return record;
    }

    public Task<IReadOnlyCollection<OrganizationInvitationRecord>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
        => _store.ListAsync(organizationId, cancellationToken);

    public Task<PagedResult<OrganizationInvitationRecord>> ListAsync(
        Guid organizationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
        => _store.ListAsync(organizationId, pageRequest, cancellationToken);

    public async Task<bool> RevokeAsync(Guid organizationId, Guid code, CancellationToken cancellationToken = default)
    {
        var invitation = await _store.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (invitation is null || invitation.OrganizationId != organizationId)
        {
            return false;
        }

        var lifecycleContext = new OrganizationLifecycleContext(
            OrganizationLifecycleEvent.InvitationRevoked,
            organizationId,
            null,
            null,
            Invitation: invitation);

        await _lifecycleDispatcher.EnsureCanRevokeInvitationAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);

        await _store.RemoveAsync(code, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Revoked invitation {InvitationCode} for organization {OrganizationId}.",
            code,
            organizationId);

        await _lifecycleDispatcher.NotifyInvitationRevokedAsync(lifecycleContext, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<OrganizationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
        => _store.FindAsync(code, cancellationToken);

    public async Task<OrganizationInvitationAcceptanceResult?> AcceptAsync(Guid code, ApplicationUser user, CancellationToken cancellationToken = default)
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

        var wasExistingUser = user.CreatedAt <= invitation.CreatedAtUtc;

        var membership = await _membershipService.GetMembershipAsync(invitation.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        var roleIds = invitation.RoleIds ?? Array.Empty<Guid>();

        var acceptanceContext = new OrganizationLifecycleContext(
            OrganizationLifecycleEvent.InvitationAccepted,
            organization.Id,
            organization.Slug,
            organization.DisplayName,
            TargetUserId: user.Id,
            Organization: organization,
            Invitation: invitation);

        await _lifecycleDispatcher.EnsureCanAcceptInvitationAsync(acceptanceContext, cancellationToken).ConfigureAwait(false);

        if (membership is null)
        {
            await _membershipService.AddMemberAsync(new OrganizationMembershipRequest
            {
                OrganizationId = invitation.OrganizationId,
                UserId = user.Id,
                TenantId = organization.TenantId,
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

        await _lifecycleDispatcher.NotifyInvitationAcceptedAsync(acceptanceContext, cancellationToken).ConfigureAwait(false);

        return new OrganizationInvitationAcceptanceResult
        {
            OrganizationId = organization.Id,
            OrganizationSlug = organization.Slug,
            OrganizationName = organization.DisplayName,
            RoleIds = roleIds,
            WasExistingMember = membership is not null,
            WasExistingUser = wasExistingUser
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

            if (role.OrganizationId != Guid.Empty && role.OrganizationId != organization.Id)
            {
                throw new InvalidOperationException("One or more roles are not valid for this organization.");
            }
        }
    }
}
