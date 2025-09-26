using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Identity.Base.AspNet;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Checks if the user has the specified scope in their JWT token claims.
    /// Supports multiple scope claim formats commonly used in JWT tokens.
    /// </summary>
    /// <param name="user">The claims principal representing the authenticated user</param>
    /// <param name="requiredScope">The scope to check for (e.g., "identity.api")</param>
    /// <returns>True if the user has the required scope, false otherwise</returns>
    public static bool HasScope(this ClaimsPrincipal user, string requiredScope)
    {
        // Option 1: Check for "scope" claim with space-separated values
        var scopeClaim = user.FindFirst("scope")?.Value;
        if (!string.IsNullOrEmpty(scopeClaim) && scopeClaim.Split(' ').Contains(requiredScope))
        {
            return true;
        }

        // Option 2: Check for multiple "scope" claims
        var scopes = user.FindAll("scope").Select(c => c.Value);
        if (scopes.Contains(requiredScope))
        {
            return true;
        }

        // Option 3: Check for "scp" claim (common in some JWT implementations)
        var scpClaim = user.FindFirst("scp")?.Value;
        if (!string.IsNullOrEmpty(scpClaim) && scpClaim.Split(' ').Contains(requiredScope))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates an authorization policy that requires the user to have a specific scope.
    /// This method encapsulates the common pattern of checking for scopes in JWT tokens.
    /// </summary>
    /// <param name="policy">The authorization policy builder</param>
    /// <param name="requiredScope">The scope to check for (e.g., "identity.api")</param>
    /// <returns>The authorization policy builder for method chaining</returns>
    public static AuthorizationPolicyBuilder RequireScope(this AuthorizationPolicyBuilder policy, string requiredScope)
    {
        return policy.RequireAssertion(context => context.User.HasScope(requiredScope));
    }
}