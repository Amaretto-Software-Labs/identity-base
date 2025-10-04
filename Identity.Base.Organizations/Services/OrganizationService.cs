using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

public sealed class OrganizationService : IOrganizationService
{
    private static readonly Regex SlugRegex = new("^[a-z0-9][a-z0-9-_.]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrganizationDbContext _dbContext;
    private readonly OrganizationOptions _options;
    private readonly ILogger<OrganizationService>? _logger;

    public OrganizationService(
        OrganizationDbContext dbContext,
        IOptions<OrganizationOptions> options,
        ILogger<OrganizationService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<Organization> CreateAsync(OrganizationCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = request.TenantId;
        var slug = NormalizeSlug(request.Slug);
        var displayName = NormalizeDisplayName(request.DisplayName);
        var metadata = NormalizeMetadata(request.Metadata);

        await EnsureSlugIsUniqueAsync(tenantId, slug, cancellationToken).ConfigureAwait(false);
        await EnsureDisplayNameIsUniqueAsync(tenantId, displayName, cancellationToken).ConfigureAwait(false);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            DisplayName = displayName,
            Metadata = metadata,
            Status = OrganizationStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.Organizations.Add(organization);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Created organization {OrganizationId} (Slug: {Slug})", organization.Id, organization.Slug);
        return organization;
    }

    public async Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(organization => organization.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Organization?> GetBySlugAsync(Guid? tenantId, string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        slug = NormalizeSlug(slug);

        return await QueryByTenant(_dbContext.Organizations.AsNoTracking(), tenantId)
            .FirstOrDefaultAsync(organization => organization.Slug == slug, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Organization>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        return await QueryByTenant(_dbContext.Organizations.AsNoTracking(), tenantId)
            .OrderBy(org => org.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Organization> UpdateAsync(Guid organizationId, OrganizationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization identifier is required.", nameof(organizationId));
        }

        ArgumentNullException.ThrowIfNull(request);

        var organization = await _dbContext.Organizations
            .FirstOrDefaultAsync(entity => entity.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            throw new KeyNotFoundException($"Organization {organizationId} was not found.");
        }

        var hasChanges = false;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            var displayName = NormalizeDisplayName(request.DisplayName);
            if (!string.Equals(displayName, organization.DisplayName, StringComparison.Ordinal))
            {
                await EnsureDisplayNameIsUniqueAsync(organization.TenantId, displayName, cancellationToken, organization.Id)
                    .ConfigureAwait(false);
                organization.DisplayName = displayName;
                hasChanges = true;
            }
        }

        if (request.Metadata is not null)
        {
            var metadata = NormalizeMetadata(request.Metadata);
            if (!ReferenceEquals(metadata, organization.Metadata) && !MetadataEquals(metadata, organization.Metadata))
            {
                organization.Metadata = metadata;
                hasChanges = true;
            }
        }

        if (request.Status.HasValue && request.Status.Value != organization.Status)
        {
            organization.Status = request.Status.Value;
            organization.ArchivedAtUtc = request.Status.Value == OrganizationStatus.Archived
                ? DateTimeOffset.UtcNow
                : null;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return organization;
        }

        organization.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Updated organization {OrganizationId}", organization.Id);
        return organization;
    }

    public async Task ArchiveAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await _dbContext.Organizations
            .FirstOrDefaultAsync(entity => entity.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            throw new KeyNotFoundException($"Organization {organizationId} was not found.");
        }

        if (organization.Status == OrganizationStatus.Archived)
        {
            return;
        }

        organization.Status = OrganizationStatus.Archived;
        organization.ArchivedAtUtc = DateTimeOffset.UtcNow;
        organization.UpdatedAtUtc = organization.ArchivedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger?.LogInformation("Archived organization {OrganizationId}", organization.Id);
    }

    private string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Organization slug is required.", nameof(slug));
        }

        slug = slug.Trim().ToLowerInvariant();
        if (slug.Length > _options.SlugMaxLength)
        {
            throw new ArgumentException($"Organization slug cannot exceed {_options.SlugMaxLength} characters.", nameof(slug));
        }

        if (!SlugRegex.IsMatch(slug))
        {
            throw new ArgumentException("Organization slug may only contain lowercase letters, numbers, hyphens, underscores, and periods, and must start with a letter or number.", nameof(slug));
        }

        return slug;
    }

    private string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Organization display name is required.", nameof(displayName));
        }

        displayName = displayName.Trim();
        if (displayName.Length > _options.DisplayNameMaxLength)
        {
            throw new ArgumentException($"Organization display name cannot exceed {_options.DisplayNameMaxLength} characters.", nameof(displayName));
        }

        return displayName;
    }

    private OrganizationMetadata NormalizeMetadata(OrganizationMetadata? metadata)
    {
        metadata ??= OrganizationMetadata.Empty;

        ValidateMetadata(metadata);
        return metadata;
    }

    private void ValidateMetadata(OrganizationMetadata metadata)
    {
        foreach (var kvp in metadata.Values)
        {
            if (kvp.Key.Length > _options.MetadataMaxKeyLength)
            {
                throw new ArgumentException($"Metadata key '{kvp.Key}' exceeds the maximum length of {_options.MetadataMaxKeyLength} characters.");
            }

            if (kvp.Value is { Length: > 0 } value && value.Length > _options.MetadataMaxValueLength)
            {
                throw new ArgumentException($"Metadata value for key '{kvp.Key}' exceeds the maximum length of {_options.MetadataMaxValueLength} characters.");
            }
        }

        var bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(metadata.Values, JsonOptions));
        if (bytes > _options.MetadataMaxBytes)
        {
            throw new ArgumentException($"Metadata payload exceeds the maximum size of {_options.MetadataMaxBytes} bytes.");
        }
    }

    private static bool MetadataEquals(OrganizationMetadata left, OrganizationMetadata right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Values.Count != right.Values.Count)
        {
            return false;
        }

        foreach (var kvp in left.Values)
        {
            if (!right.Values.TryGetValue(kvp.Key, out var otherValue))
            {
                return false;
            }

            if (!string.Equals(kvp.Value, otherValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private async Task EnsureSlugIsUniqueAsync(Guid? tenantId, string slug, CancellationToken cancellationToken, Guid? excludingOrganizationId = null)
    {
        var query = QueryByTenant(_dbContext.Organizations.AsNoTracking(), tenantId)
            .Where(organization => organization.Slug == slug);

        if (excludingOrganizationId.HasValue)
        {
            query = query.Where(organization => organization.Id != excludingOrganizationId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"An organization with slug '{slug}' already exists for the specified tenant.");
        }
    }

    private async Task EnsureDisplayNameIsUniqueAsync(Guid? tenantId, string displayName, CancellationToken cancellationToken, Guid? excludingOrganizationId = null)
    {
        var query = QueryByTenant(_dbContext.Organizations.AsNoTracking(), tenantId)
            .Where(organization => organization.DisplayName == displayName);

        if (excludingOrganizationId.HasValue)
        {
            query = query.Where(organization => organization.Id != excludingOrganizationId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"An organization with display name '{displayName}' already exists for the specified tenant.");
        }
    }

    private static IQueryable<Organization> QueryByTenant(IQueryable<Organization> query, Guid? tenantId)
    {
        if (tenantId.HasValue)
        {
            return query.Where(organization => organization.TenantId == tenantId.Value);
        }

        return query.Where(organization => organization.TenantId == null);
    }
}
