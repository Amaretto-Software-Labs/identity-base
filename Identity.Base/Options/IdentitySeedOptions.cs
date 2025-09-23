using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Options;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public bool Enabled { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    public string? Password { get; init; }

    public string[] Roles { get; init; } = Array.Empty<string>();
}
