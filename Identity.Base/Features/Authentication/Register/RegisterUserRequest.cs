using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Features.Authentication.Register;

public sealed class RegisterUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string Password { get; init; } = string.Empty;

    public IDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
