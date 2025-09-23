using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Options;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    [Required]
    [MaxLength(512)]
    public string ConfirmationUrlTemplate { get; set; } = "https://localhost/confirm?token={token}&email={email}";

    [MinLength(0)]
    public IList<RegistrationProfileFieldOptions> ProfileFields { get; set; } = new List<RegistrationProfileFieldOptions>();
}

public sealed class RegistrationProfileFieldOptions
{
    [Required]
    [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Profile field names must be alphanumeric plus _.-")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    public bool Required { get; set; }

    [Range(1, 1024)]
    public int MaxLength { get; set; } = 256;

    public string? Pattern { get; set; }
}
