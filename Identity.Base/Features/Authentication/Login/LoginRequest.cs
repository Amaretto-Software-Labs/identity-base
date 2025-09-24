using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Features.Authentication.Login;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    public string? ClientSecret { get; init; }

    public IList<string> Scopes { get; init; } = new List<string>();
}
