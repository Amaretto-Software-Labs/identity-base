using System;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationContextAccessor
{
    IOrganizationContext Current { get; }

    IDisposable BeginScope(IOrganizationContext organizationContext);
}
