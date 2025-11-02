using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationInvitationService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan MinimumLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromDays(30);

    private readonly IOrganisationInvitationStore _store;
    private readonly IOrganisationService _organisationService;
    private readonly IOrganisationMembershipService _membershipService;
    private readonly IOrganisationRoleService _roleService;
    private readonly ILogger<OrganisationInvitationService> _logger;
    private readonly ILogSanitizer _logSanitizer;

    public OrganisationInvitationService(
        IOrganisationInvitationStore store,
        IOrganisationService organisationService,
        IOrganisationMembershipService membershipService,
        IOrganisationRoleService roleService,
        ILogger<OrganisationInvitationService> logger,
        ILogSanitizer logSanitizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _organisationService = organisationService ?? throw new ArgumentNullException(nameof(organisationService));
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logSanitizer = logSanitizer ?? throw new ArgumentNullException(nameof(logSanitizer));
    }

    public async Task<OrganisationInvitationRecord> CreateAsync(
        Guid organisationId,
        string email,
        IReadOnlyCollection<Guid> roleIds,
        Guid? createdBy,
        int? expiresInHours,
        CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(organisationId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var organisation = await _organisationService.GetByIdAsync(organisationId, cancellationToken).ConfigureAwait(false);
        if (organisation is null)
        {
            throw new KeyNotFoundException($"Organisation {organisationId} was not found.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedRoles = NormalizeRoleIds(roleIds);
        await EnsureRolesExistAsync(normalizedRoles, organisation, cancellationToken).ConfigureAwait(false);

        var hasActiveInvitation = await _store.HasActiveInvitationAsync(organisation.Id, normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (hasActiveInvitation)
        {
            throw new OrganisationInvitationAlreadyExistsException(normalizedEmail);
        }

        var lifetime = ResolveLifetime(expiresInHours);

        var record = new OrganisationInvitationRecord
        {
            Code = Guid.NewGuid(),
            OrganisationId = organisation.Id,
            OrganisationSlug = organisation.Slug,
            OrganisationName = organisation.DisplayName,
            Email = normalizedEmail,
            RoleIds = normalizedRoles.ToArray(),
            CreatedBy = createdBy,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(lifetime)
        };

        await _store.CreateAsync(record, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created invitation {InvitationCode} for organisation {OrganisationId} ({Email}).",
            record.Code,
            organisation.Id,
            _logSanitizer.RedactEmail(normalizedEmail));

        return record;
    }

    public Task<IReadOnlyCollection<OrganisationInvitationRecord>> ListAsync(Guid organisationId, CancellationToken cancellationToken = default)
        => _store.ListAsync(organisationId, cancellationToken);

    public async Task<bool> RevokeAsync(Guid organisationId, Guid code, CancellationToken cancellationToken = default)
    {
        var invitation = await _store.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (invitation is null || invitation.OrganisationId != organisationId)
        {
            return false;
        }

        await _store.RemoveAsync(code, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Revoked invitation {InvitationCode} for organisation {OrganisationId}.",
            code,
            organisationId);
        return true;
    }

    public Task<OrganisationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
        => _store.FindAsync(code, cancellationToken);

    public async Task<OrganisationInvitationAcceptanceResult?> AcceptAsync(Guid code, ApplicationUser user, CancellationToken cancellationToken = default)
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

        var organisation = await _organisationService.GetByIdAsync(invitation.OrganisationId, cancellationToken).ConfigureAwait(false);
        if (organisation is null)
        {
            await _store.RemoveAsync(code, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var wasExistingUser = user.CreatedAt <= invitation.CreatedAtUtc;

        var membership = await _membershipService.GetMembershipAsync(invitation.OrganisationId, user.Id, cancellationToken).ConfigureAwait(false);
        var roleIds = invitation.RoleIds ?? Array.Empty<Guid>();

        if (membership is null)
        {
            await _membershipService.AddMemberAsync(new OrganisationMembershipRequest
            {
                OrganisationId = invitation.OrganisationId,
                UserId = user.Id,
                TenantId = organisation.TenantId,
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
                await _membershipService.UpdateMembershipAsync(new OrganisationMembershipUpdateRequest
                {
                    OrganisationId = invitation.OrganisationId,
                    UserId = user.Id,
                    IsPrimary = null,
                    RoleIds = mergedRoleIds
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        await _store.RemoveAsync(code, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} accepted invitation {InvitationCode} for organisation {OrganisationId}.",
            user.Id,
            code,
            invitation.OrganisationId);

        return new OrganisationInvitationAcceptanceResult
        {
            OrganisationId = organisation.Id,
            OrganisationSlug = organisation.Slug,
            OrganisationName = organisation.DisplayName,
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

    private async Task EnsureRolesExistAsync(HashSet<Guid> roleIds, Organisation organisation, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return;
        }

        var roles = await _roleService.ListAsync(organisation.TenantId, organisation.Id, cancellationToken).ConfigureAwait(false);
        var available = roles.ToDictionary(role => role.Id);

        foreach (var roleId in roleIds)
        {
            if (!available.TryGetValue(roleId, out var role))
            {
                throw new InvalidOperationException("One or more roles are not valid for this organisation.");
            }

            if (role.OrganisationId.HasValue && role.OrganisationId != organisation.Id)
            {
                throw new InvalidOperationException("One or more roles are not valid for this organisation.");
            }
        }
    }
}
