using System;
using System.Collections.Generic;

namespace OrgSampleApi;

public sealed class OrganizationBootstrapOptions
{
    public string? Slug { get; set; }

    public string? DisplayName { get; set; }

    public Dictionary<string, string?> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

