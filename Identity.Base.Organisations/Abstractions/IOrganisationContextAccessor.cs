using System;

namespace Identity.Base.Organisations.Abstractions;

public interface IOrganisationContextAccessor
{
    IOrganisationContext Current { get; }

    IDisposable BeginScope(IOrganisationContext organisationContext);
}
