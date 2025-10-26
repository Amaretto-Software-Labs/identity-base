using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrgSampleApi;

public sealed class OrganizationBootstrapUserCreationListener : IUserCreationListener
{
    private static readonly string[] SlugKeys = { "organizationSlug", "organization.slug" };
    private static readonly string[] NameKeys = { "organizationName", "organization.name", "organizationDisplayName" };
    private const string MetadataPrefix = "organization.metadata.";

    private readonly OrganizationBootstrapService _bootstrapService;
    private readonly IOptions<OrganizationBootstrapOptions> _defaults;
    private readonly IOptions<IdentitySeedOptions> _seedOptions;
    private readonly ILogger<OrganizationBootstrapUserCreationListener> _logger;

    public OrganizationBootstrapUserCreationListener(
        OrganizationBootstrapService bootstrapService,
        IOptions<OrganizationBootstrapOptions> defaults,
        IOptions<IdentitySeedOptions> seedOptions,
        ILogger<OrganizationBootstrapUserCreationListener> logger)
    {
        _bootstrapService = bootstrapService ?? throw new ArgumentNullException(nameof(bootstrapService));
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _seedOptions = seedOptions ?? throw new ArgumentNullException(nameof(seedOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnUserCreatedAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var request = ResolveFromMetadata(user.ProfileMetadata);
        if (request is null && ShouldUseDefaults(user))
        {
            request = ResolveFromDefaults();
        }

        if (request is null)
        {
            _logger.LogDebug("No organization bootstrap metadata supplied for user {UserId}.", user.Id);
            return;
        }

        try
        {
            await _bootstrapService.EnsureOrganizationOwnerAsync(user, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Failed to ensure organization membership for user {UserId} ({Email}).",
                user.Id,
                user.Email);
        }
    }

    private static OrganizationBootstrapRequest? ResolveFromMetadata(UserProfileMetadata metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var slug = ResolveValue(metadata.Values, SlugKeys);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var displayName = ResolveValue(metadata.Values, NameKeys) ?? slug;
        var metadataValues = ExtractMetadata(metadata.Values);
        return new OrganizationBootstrapRequest(slug, displayName, metadataValues);
    }

    private static string? ResolveValue(IReadOnlyDictionary<string, string?> values, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string?> ExtractMetadata(IReadOnlyDictionary<string, string?> values)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (!key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var metadataKey = key[MetadataPrefix.Length..];
            if (string.IsNullOrWhiteSpace(metadataKey))
            {
                continue;
            }

            result[metadataKey] = value;
        }

        return result;
    }

    private bool ShouldUseDefaults(ApplicationUser user)
    {
        var options = _seedOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email))
        {
            return false;
        }

        return string.Equals(options.Email, user.Email, StringComparison.OrdinalIgnoreCase);
    }

    private OrganizationBootstrapRequest? ResolveFromDefaults()
    {
        var defaults = _defaults.Value;
        if (string.IsNullOrWhiteSpace(defaults.Slug) || string.IsNullOrWhiteSpace(defaults.DisplayName))
        {
            return null;
        }

        return new OrganizationBootstrapRequest(
            defaults.Slug,
            defaults.DisplayName,
            defaults.Metadata ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
    }
}

