using System;
using System.Collections.Generic;

namespace Identity.Base.Organisations.Domain;

public sealed class OrganisationMetadata
{
    public static OrganisationMetadata Empty => new();

    public OrganisationMetadata()
    {
        Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    public OrganisationMetadata(IDictionary<string, string?> values)
    {
        Values = new Dictionary<string, string?>(values ?? throw new ArgumentNullException(nameof(values)), StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, string?> Values { get; init; }
}
