using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationRoleService : IOrganizationRoleService
{
    private readonly OrganizationDbContext _dbContext;
    private readonly OrganizationRoleOptions _options;
    private readonly ILogger<OrganizationRoleService>? _logger;

    public OrganizationRoleService(
        OrganizationDbContext dbContext,
        IOptions<OrganizationRoleOptions> options,
        ILogger<OrganizationRoleService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<OrganizationRole> CreateAsync(OrganizationRoleCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Role name is required.", nameof(request));
        }

        var name = request.Name.Trim();
        if (name.Length > _options.NameMaxLength)
        {
            throw new ArgumentException($"Role name cannot exceed {_options.NameMaxLength} characters.", nameof(request));
        }

        if (request.Description is { Length: > 0 } description && description.Length > _options.DescriptionMaxLength)
        {
            throw new ArgumentException($"Role description cannot exceed {_options.DescriptionMaxLength} characters.", nameof(request));
        }

        var tenantId = request.TenantId;
        if (request.OrganizationId.HasValue)
        {
            var organization = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(org => org.Id == request.OrganizationId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (organization is null)
            {
                throw new KeyNotFoundException($"Organization {request.OrganizationId.Value} was not found.");
            }

            if (tenantId.HasValue && organization.TenantId.HasValue && tenantId.Value != organization.TenantId.Value)
            {
                throw new InvalidOperationException("Organization and role tenants do not match.");
            }

            tenantId ??= organization.TenantId;
        }

        await EnsureRoleNameIsUniqueAsync(tenantId, request.OrganizationId, name, cancellationToken).ConfigureAwait(false);

        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            TenantId = tenantId,
            Name = name,
            Description = request.Description?.Trim(),
            IsSystemRole = request.IsSystemRole,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.OrganizationRoles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation(
            "Created organization role {RoleId} (Name: {RoleName}) for organization {OrganizationId}",
            role.Id,
            role.Name,
            role.OrganizationId);

        return role;
    }

    public async Task<OrganizationRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.OrganizationRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Id == roleId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OrganizationRole>> ListAsync(Guid? tenantId, Guid? organizationId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OrganizationRoles.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(role => role.TenantId == tenantId.Value || role.TenantId == null);
        }
        else
        {
            query = query.Where(role => role.TenantId == null);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(role => role.OrganizationId == organizationId.Value || role.OrganizationId == null);
        }

        return await query
            .OrderBy(role => role.OrganizationId.HasValue ? 1 : 0)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        var role = await _dbContext.OrganizationRoles
            .FirstOrDefaultAsync(entity => entity.Id == roleId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return;
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("System roles cannot be deleted.");
        }

        _dbContext.OrganizationRoles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Deleted organization role {RoleId}", roleId);
    }

    private async Task EnsureRoleNameIsUniqueAsync(Guid? tenantId, Guid? organizationId, string roleName, CancellationToken cancellationToken)
    {
        var query = _dbContext.OrganizationRoles.AsNoTracking()
            .Where(role => role.Name == roleName);

        if (tenantId.HasValue)
        {
            query = query.Where(role => role.TenantId == tenantId.Value);
        }
        else
        {
            query = query.Where(role => role.TenantId == null);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(role => role.OrganizationId == organizationId.Value);
        }
        else
        {
            query = query.Where(role => role.OrganizationId == null);
        }

        var exists = await query.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Role '{roleName}' already exists for the specified scope.");
        }
    }
}
