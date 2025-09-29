using System;

namespace Identity.Base.Abstractions.MultiTenancy;

/// <summary>
/// Provides access to the current tenant context and allows scoped overrides.
/// </summary>
public interface ITenantContextAccessor
{
    /// <summary>
    /// Gets the active tenant context.
    /// </summary>
    ITenantContext Current { get; }

    /// <summary>
    /// Applies the specified tenant context for the current logical execution scope.
    /// </summary>
    /// <param name="tenantContext">The tenant context to apply.</param>
    /// <returns>
    /// A disposable scope that reverts the tenant context when disposed. The scope should be disposed
    /// at the end of the logical operation (e.g., end of an HTTP request).
    /// </returns>
    IDisposable BeginScope(ITenantContext tenantContext);
}
