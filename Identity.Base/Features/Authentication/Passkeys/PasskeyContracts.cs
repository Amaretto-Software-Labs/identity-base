using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed record PasskeyConfigurationResponse(
    bool Enabled,
    bool Usernameless,
    bool ConditionalUi,
    string UserVerification,
    IReadOnlyCollection<string> SignupModes,
    bool SignupEmailVerificationRequired);

internal sealed class PasskeyOptionsRequest
{
    [Required]
    public string ClientId { get; init; } = string.Empty;
}

internal sealed class PasskeyAuthenticationRequest
{
    [Required]
    public string ClientId { get; init; } = string.Empty;

    public JsonElement Credential { get; init; }
}

internal sealed class PasskeyCreationRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public JsonElement Credential { get; init; }
}

internal sealed class PasskeyRenameRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string ConcurrencyStamp { get; init; } = string.Empty;
}

internal sealed class PasskeySignupBeginRequest
{
    [Required]
    public string Mode { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    public IDictionary<string, string?> Metadata { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class PasskeyEmailConfirmationRequest
{
    public Guid DraftId { get; init; }

    [Required]
    public string Token { get; init; } = string.Empty;
}

internal sealed class PasskeyDraftRequest
{
    public Guid DraftId { get; init; }
}

internal sealed class PasskeySignupCompleteRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public string? Password { get; init; }

    public JsonElement Credential { get; init; }
}

internal sealed class PasskeyRecoveryBeginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;
}

internal sealed record PasskeySummary(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> Transports,
    bool IsBackupEligible,
    bool IsBackedUp,
    string ConcurrencyStamp);

internal static class PasskeyEncoding
{
    public static string CredentialId(byte[] value)
        => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(value);

    public static bool TryCredentialId(string value, out byte[] credentialId)
    {
        try
        {
            credentialId = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(value);
            return credentialId.Length is > 0 and <= 1023;
        }
        catch (FormatException)
        {
            credentialId = [];
            return false;
        }
    }

    public static string Token(byte[] value)
        => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(value);

    public static bool TryToken(string value, out byte[] token)
    {
        try
        {
            token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(value);
            return token.Length == 32;
        }
        catch (FormatException)
        {
            token = [];
            return false;
        }
    }
}
