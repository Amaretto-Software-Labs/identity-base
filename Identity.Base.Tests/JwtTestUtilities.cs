using System;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Base.Tests;

internal static class JwtTestUtilities
{
    public static byte[] Base64UrlDecode(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        switch (value.Length % 4)
        {
            case 2:
                value += "==";
                break;
            case 3:
                value += "=";
                break;
        }

        return Convert.FromBase64String(value);
    }

    public static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

internal sealed record PkceData(string CodeVerifier, string CodeChallenge)
{
    public static PkceData Create()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var codeVerifier = JwtTestUtilities.Base64UrlEncode(verifierBytes);

        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = JwtTestUtilities.Base64UrlEncode(challengeBytes);

        return new PkceData(codeVerifier, codeChallenge);
    }
}
