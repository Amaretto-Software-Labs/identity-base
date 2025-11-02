using System.Collections.Generic;

namespace OrgSampleApi;

public sealed record OrganizationBootstrapRequest(
    string Name,
    string Slug,
    IReadOnlyDictionary<string, string?> Metadata);
