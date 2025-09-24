using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OpenIddict.Abstractions;

namespace Identity.Base.Options;

public sealed class OpenIddictOptions
{
    public const string SectionName = "OpenIddict";

    public IList<OpenIddictApplicationOptions> Applications { get; init; } = new List<OpenIddictApplicationOptions>();

    public IList<OpenIddictScopeOptions> Scopes { get; init; } = new List<OpenIddictScopeOptions>();
}

public sealed class OpenIddictApplicationOptions
{
    [Required]
    public string ClientId { get; init; } = string.Empty;

    public string? ClientSecret { get; init; }

    [Required]
    public string ClientType { get; init; } = OpenIddictConstants.ClientTypes.Public;

    public IList<string> RedirectUris { get; init; } = new List<string>();

    public IList<string> PostLogoutRedirectUris { get; init; } = new List<string>();

    public IList<string> Permissions { get; init; } = new List<string>();

    public IList<string> Requirements { get; init; } = new List<string>();
}

public sealed class OpenIddictScopeOptions
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public IList<string> Resources { get; init; } = new List<string>();
}
