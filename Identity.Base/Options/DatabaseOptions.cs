using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required]
    public string? Primary { get; init; }
}
