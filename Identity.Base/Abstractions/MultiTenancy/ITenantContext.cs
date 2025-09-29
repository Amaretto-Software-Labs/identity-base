using System;
using System.Collections.Generic;

namespace Identity.Base.Abstractions.MultiTenancy;

/// <summary>
/// Represents the resolved tenant for the current execution context.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets a value indicating whether a tenant has been resolved.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Gets the unique identifier for the tenant when available.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Gets the stable tenant key/slug when available.
    /// </summary>
    string? TenantKey { get; }

    /// <summary>
    /// Gets the human-readable tenant name.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Gets arbitrary metadata associated with the tenant context.
    /// </summary>
    IReadOnlyDictionary<string, string?> Metadata { get; }

    /// <summary>
    /// Retrieves a metadata value for the specified key.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <returns>The associated metadata value, or <c>null</c> when not present.</returns>
    string? this[string key] { get; }
}
