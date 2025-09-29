using System;

namespace Identity.Base.Abstractions.MultiTenancy;

/// <summary>
/// Default accessor used when multi-tenancy is not enabled. Always exposes <see cref="TenantContext.None"/>.
/// </summary>
public sealed class NullTenantContextAccessor : ITenantContextAccessor
{
    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose()
        {
            // nothing to clean up
        }
    }

    public ITenantContext Current => TenantContext.None;

    public IDisposable BeginScope(ITenantContext tenantContext) => NullScope.Instance;
}
