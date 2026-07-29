using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed record PasskeyDraftState(
    Guid DraftId,
    Guid UserId,
    string Mode,
    string ClientId,
    DateTimeOffset ExpiresAt);

internal sealed class PasskeyStateProtector
{
    private const string RegistrationCookie = "Identity.PasskeyRegistration";
    private const string RecoveryCookie = "Identity.PasskeyRecovery";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDataProtector _registrationProtector;
    private readonly IDataProtector _recoveryProtector;

    public PasskeyStateProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _registrationProtector = dataProtectionProvider.CreateProtector(
            "Identity.Base.Passkeys.Registration.v1");
        _recoveryProtector = dataProtectionProvider.CreateProtector(
            "Identity.Base.Passkeys.Recovery.v1");
    }

    public void WriteRegistration(HttpResponse response, PasskeyDraftState state)
        => Write(response, RegistrationCookie, _registrationProtector, state);

    public void WriteRecovery(HttpResponse response, PasskeyDraftState state)
        => Write(response, RecoveryCookie, _recoveryProtector, state);

    public PasskeyDraftState? ReadRegistration(HttpRequest request)
        => Read(request, RegistrationCookie, _registrationProtector);

    public PasskeyDraftState? ReadRecovery(HttpRequest request)
        => Read(request, RecoveryCookie, _recoveryProtector);

    public void ClearRegistration(HttpResponse response)
        => response.Cookies.Delete(RegistrationCookie);

    public void ClearRecovery(HttpResponse response)
        => response.Cookies.Delete(RecoveryCookie);

    private static void Write(
        HttpResponse response,
        string cookieName,
        IDataProtector protector,
        PasskeyDraftState state)
    {
        var protectedValue = protector.Protect(JsonSerializer.Serialize(state, JsonOptions));
        response.Cookies.Append(cookieName, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = state.ExpiresAt
        });
    }

    private static PasskeyDraftState? Read(
        HttpRequest request,
        string cookieName,
        IDataProtector protector)
    {
        if (!request.Cookies.TryGetValue(cookieName, out var protectedValue))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<PasskeyDraftState>(
                protector.Unprotect(protectedValue),
                JsonOptions);
            return state is not null && state.ExpiresAt > DateTimeOffset.UtcNow
                ? state
                : null;
        }
        catch (Exception exception) when (
            exception is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return null;
        }
    }
}
