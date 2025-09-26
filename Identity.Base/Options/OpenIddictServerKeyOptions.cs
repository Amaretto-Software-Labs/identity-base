using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Options;

public sealed class OpenIddictServerKeyOptions
{
    public const string SectionName = "OpenIddict:ServerKeys";

    public const string ProviderDevelopment = "Development";
    public const string ProviderFileSystem = "File";
    public const string ProviderAzureKeyVault = "AzureKeyVault";

    [RegularExpression("Development|File|AzureKeyVault", ErrorMessage = "OpenIddict server key provider must be 'Development', 'File', or 'AzureKeyVault'.")]
    public string Provider { get; set; } = ProviderDevelopment;

    public FileCertificateOptions File { get; init; } = new();

    public AzureKeyVaultCertificateOptions AzureKeyVault { get; init; } = new();
}

public sealed class FileCertificateOptions
{
    public CertificateDescriptor Signing { get; init; } = new();

    public CertificateDescriptor? Encryption { get; init; }
}

public sealed class CertificateDescriptor
{
    public string Path { get; init; } = string.Empty;

    public string? Password { get; init; }
}

public sealed class AzureKeyVaultCertificateOptions
{
    [Url]
    public string VaultUri { get; init; } = string.Empty;

    public string SigningSecretName { get; init; } = string.Empty;

    public string? SigningSecretVersion { get; init; }

    public string? SigningSecretPassword { get; init; }

    public string? EncryptionSecretName { get; init; }

    public string? EncryptionSecretVersion { get; init; }

    public string? EncryptionSecretPassword { get; init; }

    public string? ManagedIdentityClientId { get; init; }
}
