using System;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;

namespace Identity.Base.Organisations.Services;

public sealed class DefaultOrganisationContextAccessor : IOrganisationContextAccessor
{
    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose()
        {
        }
    }

    public IOrganisationContext Current => OrganisationContext.None;

    public IDisposable BeginScope(IOrganisationContext organisationContext) => NullScope.Instance;
}
