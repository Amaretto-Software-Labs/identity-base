using System;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Services;

public sealed class DefaultOrganizationContextAccessor : IOrganizationContextAccessor
{
    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose()
        {
        }
    }

    public IOrganizationContext Current => OrganizationContext.None;

    public IDisposable BeginScope(IOrganizationContext organizationContext) => NullScope.Instance;
}
