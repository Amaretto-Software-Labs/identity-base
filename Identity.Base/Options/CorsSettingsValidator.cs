using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

public sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        if (options.AllowedOrigins is null || options.AllowedOrigins.Count == 0)
        {
            return ValidateOptionsResult.Fail("Cors:AllowedOrigins must contain at least one origin.");
        }

        foreach (var origin in options.AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return ValidateOptionsResult.Fail($"CORS origin '{origin}' is not a valid http/https URI.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
