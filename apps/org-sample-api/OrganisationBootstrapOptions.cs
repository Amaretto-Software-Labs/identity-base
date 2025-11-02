using System;
using System.Collections.Generic;

namespace OrgSampleApi;

public sealed class OrganisationBootstrapOptions
{
    public string? Slug { get; set; }

    public string? DisplayName { get; set; }

    public Dictionary<string, string?> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

