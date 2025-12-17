using System;
using System.Collections.Generic;
using Identity.Base.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.KeyManagement;

public static class OpenIddictServerBuilderExtensions
{
    private static readonly object _syncRoot = new();

    private static readonly Dictionary<string, OpenIddictServerKeyConfigurator> _configurators =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [OpenIddictServerKeyOptions.ProviderDevelopment] = ConfigureDevelopmentKeys,
            [OpenIddictServerKeyOptions.ProviderFileSystem] = ConfigureFileSystemKeys,
            [OpenIddictServerKeyOptions.ProviderAzureKeyVault] = ConfigureAzureKeyVaultKeys,
        };

    public static void RegisterServerKeyProvider(string providerName, OpenIddictServerKeyConfigurator configurator)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentNullException.ThrowIfNull(configurator);

        lock (_syncRoot)
        {
            _configurators[providerName] = configurator;
        }
    }

    public static OpenIddictServerBuilder UseConfiguredServerKeys(
        this OpenIddictServerBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var options = configuration.GetSection(OpenIddictServerKeyOptions.SectionName).Get<OpenIddictServerKeyOptions>()
            ?? new OpenIddictServerKeyOptions();

        var provider = options.Provider ?? OpenIddictServerKeyOptions.ProviderDevelopment;

        OpenIddictServerKeyConfigurator configurator;
        lock (_syncRoot)
        {
            if (!_configurators.TryGetValue(provider, out configurator!))
            {
                throw new InvalidOperationException($"Unsupported OpenIddict server key provider: '{provider}'.");
            }
        }

        configurator(builder, options, environment);
        return builder;
    }

    private static void ConfigureDevelopmentKeys(OpenIddictServerBuilder builder, OpenIddictServerKeyOptions options, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "OpenIddict server key provider 'Development' can only be used when ASPNETCORE_ENVIRONMENT=Development. " +
                "Configure a persisted key provider such as 'FileSystem' or 'AzureKeyVault' instead.");
        }

        builder.AddDevelopmentSigningCertificate();
        builder.AddDevelopmentEncryptionCertificate();
    }

    private static void ConfigureFileSystemKeys(OpenIddictServerBuilder builder, OpenIddictServerKeyOptions options, IHostEnvironment environment)
    {
        ConfigureFromProvider(builder, new FileSystemOpenIddictServerKeyProvider(options.File));
    }

    private static void ConfigureAzureKeyVaultKeys(OpenIddictServerBuilder builder, OpenIddictServerKeyOptions options, IHostEnvironment environment)
    {
        ConfigureFromProvider(builder, new AzureKeyVaultOpenIddictServerKeyProvider(options.AzureKeyVault));
    }

    private static void ConfigureFromProvider(OpenIddictServerBuilder builder, IOpenIddictServerKeyProvider provider)
    {
        var signing = provider.GetSigningCertificate();
        if (signing is null)
        {
            throw new InvalidOperationException("OpenIddict server key provider did not return a signing certificate.");
        }

        builder.AddSigningCertificate(signing);

        var encryption = provider.GetEncryptionCertificate();
        if (encryption is not null)
        {
            builder.AddEncryptionCertificate(encryption);
        }
    }
}

public delegate void OpenIddictServerKeyConfigurator(
    OpenIddictServerBuilder builder,
    OpenIddictServerKeyOptions options,
    IHostEnvironment environment);
