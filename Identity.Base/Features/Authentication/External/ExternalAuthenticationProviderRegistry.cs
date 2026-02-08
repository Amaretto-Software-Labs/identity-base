using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Identity.Base.Features.Authentication.External;

internal interface IExternalAuthenticationProviderRegistry
{
    void Register(string provider, string scheme);

    bool TryResolve(string provider, out ExternalAuthenticationProviderRegistration registration);
}

internal sealed class ExternalAuthenticationProviderRegistry : IExternalAuthenticationProviderRegistry
{
    private readonly ConcurrentDictionary<string, ExternalAuthenticationProviderRegistration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(string provider, string scheme)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider identifier is required.", nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(scheme))
        {
            throw new ArgumentException("Authentication scheme is required.", nameof(scheme));
        }

        var normalizedProvider = provider.Trim();
        var normalizedScheme = scheme.Trim();
        _registrations[normalizedProvider] = new ExternalAuthenticationProviderRegistration(normalizedProvider, normalizedScheme);
    }

    public bool TryResolve(string provider, out ExternalAuthenticationProviderRegistration registration)
    {
        registration = default;

        if (string.IsNullOrWhiteSpace(provider))
        {
            return false;
        }

        return _registrations.TryGetValue(provider.Trim(), out registration);
    }
}

internal readonly record struct ExternalAuthenticationProviderRegistration(string Provider, string Scheme);
