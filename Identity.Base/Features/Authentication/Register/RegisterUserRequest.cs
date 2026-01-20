using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Features.Authentication.Register;

internal sealed class RegisterUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public IDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
