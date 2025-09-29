using System;
using System.Collections.Generic;

namespace Identity.Base.Abstractions.MultiTenancy;

/// <summary>
/// Default tenant context implementation used by the portal layer to provide tenant metadata.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyMetadata = new Dictionary<string, string?>();

    /// <summary>
    /// Gets an empty tenant context representing the absence of a tenant.
    /// </summary>
    public static TenantContext None { get; } = new(null, null, null);

    public TenantContext(Guid? tenantId, string? tenantKey, string? displayName)
        : this(tenantId, tenantKey, displayName, metadata: null)
    {
    }

    public TenantContext(
        Guid? tenantId,
        string? tenantKey,
        string? displayName,
        IReadOnlyDictionary<string, string?>? metadata)
    {
        TenantId = tenantId;
        TenantKey = tenantKey;
        DisplayName = displayName;
        Metadata = metadata ?? EmptyMetadata;
    }

    public bool HasTenant => TenantId.HasValue || !string.IsNullOrWhiteSpace(TenantKey);

    public Guid? TenantId { get; }

    public string? TenantKey { get; }

    public string? DisplayName { get; }

    public IReadOnlyDictionary<string, string?> Metadata { get; }

    public string? this[string key] => Metadata.TryGetValue(key, out var value) ? value : null;
}
