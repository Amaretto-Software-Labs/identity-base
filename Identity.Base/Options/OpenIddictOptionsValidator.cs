using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Identity.Base.Options;

internal sealed class OpenIddictOptionsValidator : IValidateOptions<OpenIddictOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenIddictOptions options)
    {
        if (options.Applications is null || options.Applications.Count == 0)
        {
            return ValidateOptionsResult.Fail("At least one OpenIddict application must be configured.");
        }

        var clientIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var application in options.Applications)
        {
            if (string.IsNullOrWhiteSpace(application.ClientId))
            {
                return ValidateOptionsResult.Fail("OpenIddict application ClientId is required.");
            }

            if (!clientIds.Add(application.ClientId))
            {
                return ValidateOptionsResult.Fail($"OpenIddict application ClientId '{application.ClientId}' is duplicated.");
            }

            if (application.ClientType == OpenIddictConstants.ClientTypes.Confidential && string.IsNullOrWhiteSpace(application.ClientSecret))
            {
                return ValidateOptionsResult.Fail($"Client secret must be provided for confidential client '{application.ClientId}'.");
            }

            if (application.AllowClientCredentialsFlow && application.ClientType != OpenIddictConstants.ClientTypes.Confidential)
            {
                return ValidateOptionsResult.Fail($"Client credentials flow can only be enabled for confidential clients (client '{application.ClientId}').");
            }
        }

        if (options.Scopes is not null)
        {
            var scopes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scope in options.Scopes)
            {
                if (string.IsNullOrWhiteSpace(scope.Name))
                {
                    return ValidateOptionsResult.Fail("OpenIddict scope name is required.");
                }

                if (!scopes.Add(scope.Name))
                {
                    return ValidateOptionsResult.Fail($"OpenIddict scope '{scope.Name}' is duplicated.");
                }
            }
        }

        return ValidateOptionsResult.Success;
    }
}
