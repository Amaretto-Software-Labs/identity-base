using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Tests.Fakes;

public sealed class FakeExternalAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public FakeExternalAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var email = Context.Request.Query["email"].FirstOrDefault() ?? "external.user@example.com";
        var displayName = Context.Request.Query["name"].FirstOrDefault() ?? "External User";
        var providerKey = Context.Request.Query["key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        var emailVerified = Context.Request.Query["emailVerified"].FirstOrDefault();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, providerKey),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, displayName)
        };

        if (!string.IsNullOrWhiteSpace(emailVerified))
        {
            claims.Add(new Claim("email_verified", emailVerified));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties(properties?.Items ?? new Dictionary<string, string?>())
        {
            RedirectUri = properties?.RedirectUri ?? "/"
        };

        Context.SignInAsync(IdentityConstants.ExternalScheme, principal, authProperties);
        Context.Response.Redirect(authProperties.RedirectUri);
        return Task.CompletedTask;
    }
}
