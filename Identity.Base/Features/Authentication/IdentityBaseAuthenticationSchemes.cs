using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Validation.AspNetCore;

namespace Identity.Base.Features.Authentication;

internal static class IdentityBaseAuthenticationSchemes
{
    public const string Account = "Identity.Base.Account";

    public static string SelectAccountScheme(HttpContext context)
        => context.Request.Headers.Authorization.ToString()
            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : IdentityConstants.ApplicationScheme;
}
