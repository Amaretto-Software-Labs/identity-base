using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace Identity.Base.Tests;

public class MfaEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public MfaEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Enroll_And_Verify_EnableAuthenticatorMfa()
    {
        const string email = "mfa-enroll@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, password);

        using var enrollResponse = await client.PostAsync("/auth/mfa/enroll", null);
        enrollResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var enrollPayload = await enrollResponse.Content.ReadFromJsonAsync<JsonDocument>();
        enrollPayload.Should().NotBeNull();
        enrollPayload!.RootElement.TryGetProperty("sharedKey", out _).Should().BeTrue();
        enrollPayload.RootElement.TryGetProperty("authenticatorUri", out _).Should().BeTrue();

        var code = await GenerateAuthenticatorCodeAsync(email);

        using var verifyResponse = await client.PostAsJsonAsync("/auth/mfa/verify", new
        {
            code
        });

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await verifyResponse.Content.ReadFromJsonAsync<JsonDocument>();
        payload.Should().NotBeNull();
        payload!.RootElement.TryGetProperty("recoveryCodes", out var codesElement).Should().BeTrue();
        codesElement.GetArrayLength().Should().Be(10);

        (await GetUserAsync(email)).TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task TwoFactorLogin_FlowsThroughVerifyEndpoint()
    {
        const string email = "mfa-login@example.com";
        const string password = "StrongPass!2345";

        await EnableMfaForUserAsync(email, password);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();
        loginPayload.Should().NotBeNull();
        loginPayload!.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();
        var methods = loginPayload.RootElement.GetProperty("methods").EnumerateArray().Select(element => element!.GetString()).ToList();
        methods.Should().Contain("authenticator");
        methods.Should().Contain("sms");
        methods.Should().Contain("email");

        var code = await GenerateAuthenticatorCodeAsync(email);
        var verifyResponse = await client.PostAsJsonAsync("/auth/mfa/verify", new { code, rememberMachine = true });
        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, verifyBody);

        // After successful MFA, user should be able to hit authorize endpoint.
        var pkce = MfaPkceData.Create();
        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = "https://localhost:3000/auth/callback",
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = Guid.NewGuid().ToString("N")
        });

        using var authorizeResponse = await client.GetAsync(authorizeUrl);
        authorizeResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Disable_DisablesTwoFactor()
    {
        const string email = "mfa-disable@example.com";
        const string password = "StrongPass!2345";

        await EnableMfaForUserAsync(email, password);

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var disableResponse = await client.PostAsync("/auth/mfa/disable", null);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetUserAsync(email)).TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_ReturnsNewCodes()
    {
        const string email = "mfa-recovery@example.com";
        const string password = "StrongPass!2345";

        await EnableMfaForUserAsync(email, password);

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var initialCodes = await client.PostAsync("/auth/mfa/recovery-codes", null);
        initialCodes.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialPayload = await initialCodes.Content.ReadFromJsonAsync<JsonDocument>();
        var firstCode = initialPayload!.RootElement.GetProperty("recoveryCodes")[0].GetString();

        var secondResponse = await client.PostAsync("/auth/mfa/recovery-codes", null);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var secondFirstCode = secondPayload!.RootElement.GetProperty("recoveryCodes")[0].GetString();

        secondFirstCode.Should().NotBe(firstCode);
    }

    [Fact]
    public async Task Challenge_Sms_SendsCode()
    {
        const string email = "mfa-sms@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, confirmEmail: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var storedUser = await userManager.FindByEmailAsync(email);
            storedUser.Should().NotBeNull();
            (await userManager.SetTwoFactorEnabledAsync(storedUser!, true)).Succeeded.Should().BeTrue();
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        client.BaseAddress = new Uri("https://localhost");

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();
        loginPayload.Should().NotBeNull();
        loginPayload!.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();

        _factory.SmsChallengeSender.Clear();

        var challengeResponse = await client.PostAsJsonAsync("/auth/mfa/challenge", new { method = "sms" });
        challengeResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        _factory.SmsChallengeSender.Challenges.Should().NotBeEmpty();
        var challenge = _factory.SmsChallengeSender.Challenges.Last();

        var verifyResponse = await client.PostAsJsonAsync("/auth/mfa/verify", new { code = challenge.Code, method = "sms" });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Challenge_Email_SendsCode()
    {
        _factory.EmailSender.Clear();

        const string email = "mfa-email@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, confirmEmail: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var storedUser = await userManager.FindByEmailAsync(email);
            storedUser.Should().NotBeNull();
            (await userManager.SetTwoFactorEnabledAsync(storedUser!, true)).Succeeded.Should().BeTrue();
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        client.BaseAddress = new Uri("https://localhost");

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();
        loginPayload.Should().NotBeNull();
        loginPayload!.RootElement.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();

        var challengeResponse = await client.PostAsJsonAsync("/auth/mfa/challenge", new { method = "email" });
        var challengeBody = await challengeResponse.Content.ReadAsStringAsync();
        challengeResponse.StatusCode.Should().Be(HttpStatusCode.Accepted, challengeBody);

        _factory.EmailSender.Sent.Should().NotBeEmpty();
        var emailPayload = _factory.EmailSender.Sent.Last();
        emailPayload.Variables.Should().ContainKey("code");
        var challengeCode = emailPayload.Variables["code"]?.ToString();
        challengeCode.Should().NotBeNullOrWhiteSpace();

        var verifyResponse = await client.PostAsJsonAsync("/auth/mfa/verify", new { code = challengeCode, method = "email" });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task EnableMfaForUserAsync(string email, string password)
    {
        await SeedUserAsync(email, password, confirmEmail: true);
        using var client = await CreateAuthenticatedClientAsync(email, password);
        await client.PostAsync("/auth/mfa/enroll", null);
        var code = await GenerateAuthenticatorCodeAsync(email);
        await client.PostAsJsonAsync("/auth/mfa/verify", new { code });
    }

    private async Task<ApplicationUser> SeedUserAsync(string email, string password, bool confirmEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = confirmEmail,
                DisplayName = "MFA Test User"
            };

            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.Should().BeTrue();
            user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();
        }
        else if (confirmEmail && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        user = user ?? throw new InvalidOperationException("Failed to seed user.");

        if (confirmEmail && !await userManager.IsEmailConfirmedAsync(user))
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            var phoneResult = await userManager.SetPhoneNumberAsync(user, "+15005550006");
            phoneResult.Succeeded.Should().BeTrue();
            user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull();
        }

        if (!user!.PhoneNumberConfirmed)
        {
            user.PhoneNumberConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        return user;
    }

    private async Task<string> GenerateAuthenticatorCodeAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        var key = await userManager.GetAuthenticatorKeyAsync(user!);
        key.Should().NotBeNullOrWhiteSpace();
        return GenerateTotp(key!);
    }

    private static string GenerateTotp(string key)
    {
        using var hmac = new HMACSHA1(DecodeBase32(key));
        var timestep = GetCurrentTimeStepNumber();
        var timestepBytes = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestepBytes);
        }

        var hash = hmac.ComputeHash(timestepBytes);
        var offset = hash[^1] & 0x0F;

        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var code = binaryCode % 1_000_000;
        return code.ToString("D6");
    }

    private static long GetCurrentTimeStepNumber()
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return unixTime / 30;
    }

    private static byte[] DecodeBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleanInput = input.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=');
        cleanInput = cleanInput.ToUpperInvariant();

        var bits = 0;
        var value = 0;
        var output = new List<byte>();

        foreach (var c in cleanInput)
        {
            var index = alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new FormatException("Invalid base32 character.");
            }

            value = (value << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)(value >> bits));
                value &= (1 << bits) - 1;
            }
        }

        if (bits > 0)
        {
            output.Add((byte)(value << (8 - bits)));
        }

        return output.ToArray();
    }

    private async Task<ApplicationUser> GetUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();
        return user!;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        client.BaseAddress = new Uri("https://localhost");

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password,
            clientId = "spa-client"
        });

        loginResponse.IsSuccessStatusCode.Should().BeTrue();

        var payloadDocument = await loginResponse.Content.ReadFromJsonAsync<JsonDocument>();
        if (payloadDocument is not null &&
            payloadDocument.RootElement.TryGetProperty("requiresTwoFactor", out var requiresTwoFactorElement) &&
            requiresTwoFactorElement.ValueKind == JsonValueKind.True)
        {
            var code = await GenerateAuthenticatorCodeAsync(email);
            var verifyResponse = await client.PostAsJsonAsync("/auth/mfa/verify", new { code });
            verifyResponse.IsSuccessStatusCode.Should().BeTrue();
        }

        return client;
    }
}

internal sealed record MfaPkceData(string CodeVerifier, string CodeChallenge)
{
    public static PkceData Create()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var codeVerifier = Base64UrlEncode(verifierBytes);

        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);

        return new PkceData(codeVerifier, codeChallenge);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
