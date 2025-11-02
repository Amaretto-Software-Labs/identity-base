using System.Collections.Generic;

namespace OrgSampleApi;

public sealed record OrganisationBootstrapRequest(
    string Name,
    string Slug,
    IReadOnlyDictionary<string, string?> Metadata);
