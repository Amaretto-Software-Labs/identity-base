using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationService : IOrganisationService
{
    private static readonly Regex SlugRegex = new("^[a-z0-9][a-z0-9-_.]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrganisationDbContext _dbContext;
    private readonly OrganisationOptions _options;
    private readonly ILogger<OrganisationService>? _logger;

    public OrganisationService(
        OrganisationDbContext dbContext,
        IOptions<OrganisationOptions> options,
        ILogger<OrganisationService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<Organisation> CreateAsync(OrganisationCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = request.TenantId;
        var slug = NormalizeSlug(request.Slug);
        var displayName = NormalizeDisplayName(request.DisplayName);
        var metadata = NormalizeMetadata(request.Metadata);

        await EnsureSlugIsUniqueAsync(tenantId, slug, cancellationToken).ConfigureAwait(false);
        await EnsureDisplayNameIsUniqueAsync(tenantId, displayName, cancellationToken).ConfigureAwait(false);

        var organisation = new Organisation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            DisplayName = displayName,
            Metadata = metadata,
            Status = OrganisationStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.Organisations.Add(organisation);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Created organisation {OrganisationId} (Slug: {Slug})", organisation.Id, organisation.Slug);
        return organisation;
    }

    public async Task<Organisation?> GetByIdAsync(Guid organisationId, CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.Organisations
            .AsNoTracking()
            .FirstOrDefaultAsync(organisation => organisation.Id == organisationId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Organisation?> GetBySlugAsync(Guid? tenantId, string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        slug = NormalizeSlug(slug);

        return await QueryByTenant(_dbContext.Organisations.AsNoTracking(), tenantId)
            .FirstOrDefaultAsync(organisation => organisation.Slug == slug, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Organisation>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        return await QueryByTenant(_dbContext.Organisations.AsNoTracking(), tenantId)
            .OrderBy(org => org.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Organisation> UpdateAsync(Guid organisationId, OrganisationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier is required.", nameof(organisationId));
        }

        ArgumentNullException.ThrowIfNull(request);

        var organisation = await _dbContext.Organisations
            .FirstOrDefaultAsync(entity => entity.Id == organisationId, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
        {
            throw new KeyNotFoundException($"Organisation {organisationId} was not found.");
        }

        var hasChanges = false;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            var displayName = NormalizeDisplayName(request.DisplayName);
            if (!string.Equals(displayName, organisation.DisplayName, StringComparison.Ordinal))
            {
                await EnsureDisplayNameIsUniqueAsync(organisation.TenantId, displayName, cancellationToken, organisation.Id)
                    .ConfigureAwait(false);
                organisation.DisplayName = displayName;
                hasChanges = true;
            }
        }

        if (request.Metadata is not null)
        {
            var metadata = NormalizeMetadata(request.Metadata);
            if (!ReferenceEquals(metadata, organisation.Metadata) && !MetadataEquals(metadata, organisation.Metadata))
            {
                organisation.Metadata = metadata;
                hasChanges = true;
            }
        }

        if (request.Status.HasValue && request.Status.Value != organisation.Status)
        {
            organisation.Status = request.Status.Value;
            organisation.ArchivedAtUtc = request.Status.Value == OrganisationStatus.Archived
                ? DateTimeOffset.UtcNow
                : null;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return organisation;
        }

        organisation.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogInformation("Updated organisation {OrganisationId}", organisation.Id);
        return organisation;
    }

    public async Task ArchiveAsync(Guid organisationId, CancellationToken cancellationToken = default)
    {
        var organisation = await _dbContext.Organisations
            .FirstOrDefaultAsync(entity => entity.Id == organisationId, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
        {
            throw new KeyNotFoundException($"Organisation {organisationId} was not found.");
        }

        if (organisation.Status == OrganisationStatus.Archived)
        {
            return;
        }

        organisation.Status = OrganisationStatus.Archived;
        organisation.ArchivedAtUtc = DateTimeOffset.UtcNow;
        organisation.UpdatedAtUtc = organisation.ArchivedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger?.LogInformation("Archived organisation {OrganisationId}", organisation.Id);
    }

    private string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Organisation slug is required.", nameof(slug));
        }

        slug = slug.Trim().ToLowerInvariant();
        if (slug.Length > _options.SlugMaxLength)
        {
            throw new ArgumentException($"Organisation slug cannot exceed {_options.SlugMaxLength} characters.", nameof(slug));
        }

        if (!SlugRegex.IsMatch(slug))
        {
            throw new ArgumentException("Organisation slug may only contain lowercase letters, numbers, hyphens, underscores, and periods, and must start with a letter or number.", nameof(slug));
        }

        return slug;
    }

    private string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Organisation display name is required.", nameof(displayName));
        }

        displayName = displayName.Trim();
        if (displayName.Length > _options.DisplayNameMaxLength)
        {
            throw new ArgumentException($"Organisation display name cannot exceed {_options.DisplayNameMaxLength} characters.", nameof(displayName));
        }

        return displayName;
    }

    private OrganisationMetadata NormalizeMetadata(OrganisationMetadata? metadata)
    {
        metadata ??= OrganisationMetadata.Empty;

        ValidateMetadata(metadata);
        return metadata;
    }

    private void ValidateMetadata(OrganisationMetadata metadata)
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

    private static bool MetadataEquals(OrganisationMetadata left, OrganisationMetadata right)
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

    private async Task EnsureSlugIsUniqueAsync(Guid? tenantId, string slug, CancellationToken cancellationToken, Guid? excludingOrganisationId = null)
    {
        var query = QueryByTenant(_dbContext.Organisations.AsNoTracking(), tenantId)
            .Where(organisation => organisation.Slug == slug);

        if (excludingOrganisationId.HasValue)
        {
            query = query.Where(organisation => organisation.Id != excludingOrganisationId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"An organisation with slug '{slug}' already exists for the specified tenant.");
        }
    }

    private async Task EnsureDisplayNameIsUniqueAsync(Guid? tenantId, string displayName, CancellationToken cancellationToken, Guid? excludingOrganisationId = null)
    {
        var query = QueryByTenant(_dbContext.Organisations.AsNoTracking(), tenantId)
            .Where(organisation => organisation.DisplayName == displayName);

        if (excludingOrganisationId.HasValue)
        {
            query = query.Where(organisation => organisation.Id != excludingOrganisationId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"An organisation with display name '{displayName}' already exists for the specified tenant.");
        }
    }

    private static IQueryable<Organisation> QueryByTenant(IQueryable<Organisation> query, Guid? tenantId)
    {
        if (tenantId.HasValue)
        {
            return query.Where(organisation => organisation.TenantId == tenantId.Value);
        }

        return query.Where(organisation => organisation.TenantId == null);
    }
}
