using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Identity.Base.Options;

namespace Identity.Base.OpenIddict.KeyManagement;

internal sealed class FileSystemOpenIddictServerKeyProvider : IOpenIddictServerKeyProvider
{
    private readonly CertificateDescriptor _signing;
    private readonly CertificateDescriptor? _encryption;

    public FileSystemOpenIddictServerKeyProvider(FileCertificateOptions options)
    {
        _signing = options.Signing;
        _encryption = options.Encryption;
    }

    public X509Certificate2? GetSigningCertificate() => Load(_signing);

    public X509Certificate2? GetEncryptionCertificate() => _encryption is null ? null : Load(_encryption);

    private static X509Certificate2 Load(CertificateDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Path))
        {
            throw new InvalidOperationException("Signing certificate path must be provided when using the File provider.");
        }

        if (!File.Exists(descriptor.Path))
        {
            throw new FileNotFoundException($"Certificate file not found: {descriptor.Path}", descriptor.Path);
        }

        var password = descriptor.Password is null ? ReadOnlySpan<char>.Empty : descriptor.Password.AsSpan();
        return X509CertificateLoader.LoadPkcs12FromFile(descriptor.Path, password);
    }
}
