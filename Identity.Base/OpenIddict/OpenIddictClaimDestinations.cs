using System;
using System.Collections.Generic;
using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Identity.Base.OpenIddict;

internal static class OpenIddictClaimDestinations
{
    internal static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case OpenIddictConstants.Claims.Subject:
            case OpenIddictConstants.Claims.Email:
            case OpenIddictConstants.Claims.Name:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            default:
                if (claim.Type.StartsWith("metadata:", StringComparison.OrdinalIgnoreCase))
                {
                    yield return OpenIddictConstants.Destinations.AccessToken;
                    yield break;
                }

                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
        }
    }
}

