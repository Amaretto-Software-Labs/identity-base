using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Domain;

public sealed class OrganizationMetadata
{
    public static OrganizationMetadata Empty => new();

    public OrganizationMetadata()
    {
        Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    public OrganizationMetadata(IDictionary<string, string?> values)
    {
        Values = new Dictionary<string, string?>(values ?? throw new ArgumentNullException(nameof(values)), StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, string?> Values { get; init; }
}
