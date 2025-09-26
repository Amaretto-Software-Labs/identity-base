using System;
using System.Collections.Generic;
using System.Linq;
using Identity.Base.Options;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.External;

public interface IExternalReturnUrlValidator
{
    bool TryNormalize(string? value, out string? normalized);
}

internal sealed class ExternalReturnUrlValidator : IExternalReturnUrlValidator
{
    private readonly IOptions<OpenIddictOptions> _openIddictOptions;
    private HashSet<string>? _allowedOrigins;

    public ExternalReturnUrlValidator(IOptions<OpenIddictOptions> openIddictOptions)
    {
        _openIddictOptions = openIddictOptions;
    }

    public bool TryNormalize(string? value, out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] == '/')
        {
            if (trimmed.Length > 1 && trimmed[1] == '/')
            {
                return false;
            }

            normalized = trimmed;
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            return false;
        }

        if (!string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsAllowedOrigin(absolute))
        {
            return false;
        }

        normalized = absolute.ToString();
        return true;
    }

    private bool IsAllowedOrigin(Uri uri)
    {
        var origins = _allowedOrigins ??= BuildAllowedOrigins();
        var origin = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
        return origins.Contains(origin);
    }

    private HashSet<string> BuildAllowedOrigins()
    {
        return _openIddictOptions.Value.Applications
            .SelectMany(application => EnumerateUris(application))
            .Select(TryExtractOrigin)
            .Where(origin => origin is not null)
            .Select(origin => origin!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateUris(OpenIddictApplicationOptions application)
    {
        foreach (var uri in application.RedirectUris)
        {
            yield return uri;
        }

        if (application.PostLogoutRedirectUris is not null)
        {
            foreach (var uri in application.PostLogoutRedirectUris)
            {
                yield return uri;
            }
        }
    }

    private static string? TryExtractOrigin(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
    }
}
