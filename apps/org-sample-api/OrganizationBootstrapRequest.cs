using System.Collections.Generic;

namespace OrgSampleApi;

public sealed record OrganizationBootstrapRequest(
    string Slug,
    string DisplayName,
    IReadOnlyDictionary<string, string?> Metadata);

