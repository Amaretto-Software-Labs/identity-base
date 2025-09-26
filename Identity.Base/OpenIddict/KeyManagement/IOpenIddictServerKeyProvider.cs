using System.Security.Cryptography.X509Certificates;

namespace Identity.Base.OpenIddict.KeyManagement;

public interface IOpenIddictServerKeyProvider
{
    X509Certificate2? GetSigningCertificate();

    X509Certificate2? GetEncryptionCertificate();
}
