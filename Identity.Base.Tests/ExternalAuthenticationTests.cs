using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Shouldly;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.Lifecycle;
using Identity.Base.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace Identity.Base.Tests;

public class ExternalAuthenticationTests : IClassFixture<IdentityApiFactory>
{
    private const string ExternalWorkspaceClaimType = "urn:test:workspace";

    private readonly IdentityApiFactory _factory;

    public ExternalAuthenticationTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ExternalAuthenticationOptions_RequireVerifiedEmailForAutoLink_ByDefault()
    {
        var options = new ExternalAuthenticationOptions();

        options.RequireVerifiedEmailForAutoLinkByEmail.ShouldBeTrue();
    }

    [Fact]
    public async Task ExternalLogin_CreatesUser_WhenNew()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync("/auth/external/google/start?returnUrl=/client/callback&email=login-new@example.com&name=Login%20User");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("success");
        query["requiresTwoFactor"].ToString().ShouldBe("false");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("login-new@example.com");
        user.ShouldNotBeNull();
        var logins = await userManager.GetLoginsAsync(user!);
        logins.ShouldContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
        logins.Count(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme).ShouldBe(1);
    }

    [Fact]
    public async Task ExternalLogin_DispatchesRegistrationLifecycleHooks_WhenNew()
    {
        var probe = new ExternalRegistrationLifecycleProbe();
        using var factory = CreateLifecycleFactory<RecordingExternalRegistrationListener>(probe);
        var email = $"external-lifecycle-{Guid.NewGuid():N}@example.com";
        var providerKey = $"external-lifecycle-{Guid.NewGuid():N}";

        var query = await CompleteExternalLoginAsync(factory, email, providerKey);

        query["status"].ToString().ShouldBe("success");
        probe.BeforeRegistrationCalls.ShouldBe(1);
        probe.AfterRegistrationCalls.ShouldBe(1);
        probe.LastContext.ShouldNotBeNull();
        probe.LastContext!.Source.ShouldBe("ExternalAuthenticationService");
        probe.LastContext.Items!["Provider"].ShouldBe(IdentityApiFactory.FakeGoogleScheme);
        probe.LastContext.Items["ProviderKey"].ShouldBe(providerKey);
    }

    [Fact]
    public async Task ExternalLogin_DoesNotDispatchRegistrationLifecycleHooks_WhenAutoLinkingExistingUser()
    {
        var probe = new ExternalRegistrationLifecycleProbe();
        using var factory = CreateLifecycleFactory<RecordingExternalRegistrationListener>(probe);
        var email = $"external-existing-{Guid.NewGuid():N}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var createResult = await userManager.CreateAsync(new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Existing External User"
            });
            createResult.Succeeded.ShouldBeTrue();
        }

        var query = await CompleteExternalLoginAsync(factory, email, $"external-existing-{Guid.NewGuid():N}");

        query["status"].ToString().ShouldBe("success");
        probe.BeforeRegistrationCalls.ShouldBe(0);
        probe.AfterRegistrationCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ExternalLogin_RejectedRegistrationLifecycleHook_PreventsUserCreation()
    {
        var probe = new ExternalRegistrationLifecycleProbe();
        using var factory = CreateLifecycleFactory<RejectingExternalRegistrationListener>(probe);
        var email = $"external-rejected-{Guid.NewGuid():N}@example.com";

        var query = await CompleteExternalLoginAsync(factory, email, $"external-rejected-{Guid.NewGuid():N}");

        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("External registration disabled");
        probe.BeforeRegistrationCalls.ShouldBe(1);
        probe.AfterRegistrationCalls.ShouldBe(0);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).ShouldBeNull();
    }

    [Fact]
    public async Task ExternalLogin_FailedAssociation_DoesNotDispatchAfterHookOrLeaveCreatedUser()
    {
        var probe = new ExternalRegistrationLifecycleProbe();
        using var factory = CreateLifecycleFactory<ConflictingExternalLoginRegistrationListener>(probe);
        var email = $"external-association-failure-{Guid.NewGuid():N}@example.com";

        var query = await CompleteExternalLoginAsync(factory, email, $"external-conflict-{Guid.NewGuid():N}");

        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("associate external login");
        probe.BeforeRegistrationCalls.ShouldBe(1);
        probe.AfterRegistrationCalls.ShouldBe(0);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).ShouldBeNull();
    }

    [Fact]
    public async Task ExternalLogin_RejectsUnverifiedEmail_WithoutCreatingSessionOrUser()
    {
        var email = $"external-unverified-{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync(
            $"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Unverified%20User&emailVerified=false");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var callbackResponse = await client.GetAsync(startResponse.Headers.Location);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callbackResponse.Headers.Location.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, callbackResponse.Headers.Location);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("verified email");

        using var profileResponse = await client.GetAsync("/users/me");
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldBeNull();
    }

    [Fact]
    public async Task ExternalLogin_DoesNotLinkOrSignIn_UnconfirmedExistingUser()
    {
        var email = $"external-unconfirmed-{Guid.NewGuid():N}@example.com";
        using (var seedScope = _factory.Services.CreateScope())
        {
            var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var createResult = await userManager.CreateAsync(new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = false,
                DisplayName = "Unconfirmed External User"
            });
            createResult.Succeeded.ShouldBeTrue();
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync(
            $"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Unconfirmed%20User");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var callbackResponse = await client.GetAsync(startResponse.Headers.Location);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callbackResponse.Headers.Location.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, callbackResponse.Headers.Location);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("confirmation");

        using var profileResponse = await client.GetAsync("/users/me");
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await verifyUserManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var logins = await verifyUserManager.GetLoginsAsync(user!);
        logins.ShouldNotContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
    }

    [Fact]
    public async Task ExternalLogin_PersistsConfiguredExternalClaims()
    {
        using var claimsFactory = CreateExternalClaimPersistingFactory();
        const string email = "login-claims@example.com";
        const string providerKey = "external-claims-key";

        using var client = claimsFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var claimType = Uri.EscapeDataString(ExternalWorkspaceClaimType);
        var initialClaimValue = Uri.EscapeDataString("Initial Workspace");
        var startResponse = await client.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Login%20Claims&key={providerKey}&claimType={claimType}&claimValue={initialClaimValue}");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        using (var scope = claimsFactory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var claims = await userManager.GetClaimsAsync(user!);
            claims.ShouldContain(claim => claim.Type == ExternalWorkspaceClaimType && claim.Value == "Initial Workspace");
        }

        using var repeatClient = claimsFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        repeatClient.BaseAddress = new Uri("https://localhost");

        var updatedClaimValue = Uri.EscapeDataString("Updated Workspace");
        var repeatStartResponse = await repeatClient.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Login%20Claims&key={providerKey}&claimType={claimType}&claimValue={updatedClaimValue}");
        repeatStartResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var repeatCallbackLocation = repeatStartResponse.Headers.Location;
        repeatCallbackLocation.ShouldNotBeNull();

        var repeatCallbackResponse = await repeatClient.GetAsync(repeatCallbackLocation);
        repeatCallbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        using var currentClaimsResponse = await repeatClient.GetAsync("/test/current-external-claims");
        currentClaimsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var currentClaimsDocument = JsonDocument.Parse(await currentClaimsResponse.Content.ReadAsStringAsync());
        var currentClaims = currentClaimsDocument.RootElement
            .GetProperty("externalClaims")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        currentClaims.ShouldBe(["Updated Workspace"]);
        currentClaimsDocument.RootElement
            .GetProperty("authenticationMethod")
            .GetString()
            .ShouldBe(IdentityApiFactory.FakeGoogleScheme);

        using (var scope = claimsFactory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var claims = await userManager.GetClaimsAsync(user!);
            var workspaceClaims = claims.Where(claim => claim.Type == ExternalWorkspaceClaimType).ToList();
            workspaceClaims.Count.ShouldBe(1);
            workspaceClaims[0].Value.ShouldBe("Updated Workspace");
        }
    }

    [Fact]
    public async Task ExternalLogin_CreatesUser_ForCustomRegisteredProvider()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync("/auth/external/github/start?returnUrl=/client/callback&email=github-new@example.com&name=Github%20User");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("success");
        query["requiresTwoFactor"].ToString().ShouldBe("false");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("github-new@example.com");
        user.ShouldNotBeNull();
        var logins = await userManager.GetLoginsAsync(user!);
        logins.ShouldContain(login => string.Equals(login.LoginProvider, "GitHub", StringComparison.OrdinalIgnoreCase));
        logins.Count(login => string.Equals(login.LoginProvider, "GitHub", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
    }

    [Fact]
    public async Task ExternalLogin_DoesNotAutoLinkByEmail_WhenDisabled()
    {
        const string email = "strict-linking@example.com";
        const string password = "StrongPass!2345";

        using var strictFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["Authentication:External:AutoLinkByEmailOnLogin"] = "false"
                };
                configurationBuilder.AddInMemoryCollection(overrides);
            });
        });

        using (var scope = strictFactory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var created = await userManager.CreateAsync(new ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    DisplayName = "Strict Linking User"
                }, password);
                created.Succeeded.ShouldBeTrue();
            }
        }

        using var client = strictFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Strict%20Linking");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("not linked");

        using var verifyScope = strictFactory.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await verifyUserManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var logins = await verifyUserManager.GetLoginsAsync(user!);
        logins.ShouldNotContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
    }

    [Fact]
    public async Task ExternalLogin_RequiresVerifiedEmail_ForAutoLink_ByDefault()
    {
        const string email = "verified-required@example.com";
        const string password = "StrongPass!2345";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var created = await userManager.CreateAsync(new ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    DisplayName = "Verified Required User"
                }, password);
                created.Succeeded.ShouldBeTrue();
            }
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=Verified%20Required&emailVerified=false");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        finalLocation.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["status"].ToString().ShouldBe("error");
        query["message"].ToString().ShouldContain("not verified");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyUserManager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await verifyUserManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var logins = await verifyUserManager.GetLoginsAsync(user!);
        logins.ShouldNotContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
    }

    [Fact]
    public async Task ExternalLogin_StartAllowsConfiguredAbsoluteReturnUrl()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        // https://localhost:3000 is configured in appsettings RedirectUris
        var encoded = Uri.EscapeDataString("https://localhost:3000/auth/external-complete");
        var response = await client.GetAsync($"/auth/external/google/start?returnUrl={encoded}&email=absolute@example.com&name=Absolute");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task ExternalLogin_StartIgnoresForwardedHeaders()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/external/google/start?returnUrl=/client/callback&email=fh@example.com&name=Forwarded");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.com");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "http");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location;
        location.ShouldNotBeNull();
        location!.Host.ShouldBe("localhost");
        location.Scheme.ShouldBe("https");
    }

    [Fact]
    public async Task ExternalLogin_StartRejectsUnregisteredProvider()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var response = await client.GetAsync("/auth/external/Identity.External/start?returnUrl=/client/callback");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("https://evil.com/callback")]
    [InlineData("http://evil.com/callback")]
    [InlineData("client/callback")]
    public async Task ExternalLogin_StartRejectsUnsafeReturnUrls(string unsafeReturnUrl)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var encoded = Uri.EscapeDataString(unsafeReturnUrl);
        var response = await client.GetAsync($"/auth/external/google/start?returnUrl={encoded}&email=malicious@example.com&name=bad");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExternalLink_And_Unlink_Works()
    {
        const string email = "link-user@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        using var client = await CreateAuthenticatedClientAsync(email, password);
        var linkStart = await client.GetAsync("/auth/external/google/start?mode=link&returnUrl=/link/result&email=link@example.com&name=Linked");
        linkStart.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callback = linkStart.Headers.Location;
        callback.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callback);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        var finalUri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(finalUri.Query);
        query["status"].ToString().ShouldBe("linked");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.ShouldContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
            logins.Count(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme).ShouldBe(1);
        }

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ExternalLink_PersistsConfiguredExternalClaims()
    {
        using var claimsFactory = CreateExternalClaimPersistingFactory();
        const string email = "link-claims@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password, claimsFactory);

        using var client = await CreateAuthenticatedClientAsync(email, password, claimsFactory);
        var claimType = Uri.EscapeDataString(ExternalWorkspaceClaimType);
        var claimValue = Uri.EscapeDataString("Linked Workspace");
        var linkStart = await client.GetAsync($"/auth/external/google/start?mode=link&returnUrl=/link/result&email=link-claims-provider@example.com&name=Linked%20Claims&claimType={claimType}&claimValue={claimValue}");
        linkStart.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callback = linkStart.Headers.Location;
        callback.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callback);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        var finalUri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(finalUri.Query);
        query["status"].ToString().ShouldBe("linked");

        using var scope = claimsFactory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var claims = await userManager.GetClaimsAsync(user!);
        claims.ShouldContain(claim => claim.Type == ExternalWorkspaceClaimType && claim.Value == "Linked Workspace");
    }

    [Fact]
    public async Task ExternalLink_And_Unlink_AllowsBearerAuthentication()
    {
        const string email = "link-bearer@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        var accessToken = await _factory.CreateAccessTokenAsync(email, password, _factory, "openid profile email offline_access identity.api");

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var linkStart = await client.GetAsync("/auth/external/google/start?mode=link&returnUrl=/link/result&email=link-bearer%40example.com&name=Bearer%20Linked");
        linkStart.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callback = linkStart.Headers.Location;
        callback.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callback);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var finalLocation = callbackResponse.Headers.Location;
        var finalUri = new Uri(client.BaseAddress!, finalLocation!);
        var query = QueryHelpers.ParseQuery(finalUri.Query);
        query["status"].ToString().ShouldBe("linked");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.ShouldContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
            logins.Count(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme).ShouldBe(1);
        }

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalLink_PreparesBearerSessionBeforeBrowserNavigation()
    {
        const string cookieEmail = "link-cookie-user@example.com";
        const string bearerEmail = "link-bearer-user-navigation@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(cookieEmail, password);
        await SeedUserAsync(bearerEmail, password);

        using var client = await CreateAuthenticatedClientAsync(cookieEmail, password);
        var accessToken = await _factory.CreateAccessTokenAsync(
            bearerEmail,
            password,
            _factory,
            "openid profile email offline_access identity.api");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var prepareResponse = await client.PostAsync("/auth/external/link-session", null);
        prepareResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;
        var linkStart = await client.GetAsync(
            "/auth/external/google/start?mode=link&returnUrl=/link/result&email=navigation-provider@example.com&name=Navigation%20Linked");
        linkStart.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callback = linkStart.Headers.Location;
        callback.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callback);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cookieUser = await userManager.FindByEmailAsync(cookieEmail);
        var bearerUser = await userManager.FindByEmailAsync(bearerEmail);
        cookieUser.ShouldNotBeNull();
        bearerUser.ShouldNotBeNull();
        (await userManager.GetLoginsAsync(cookieUser!))
            .ShouldNotContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
        (await userManager.GetLoginsAsync(bearerUser!))
            .ShouldContain(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme);
    }

    [Fact]
    public async Task ExternalLink_StartRejectsClientCredentialsBearerToken()
    {
        using var tokenClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        tokenClient.BaseAddress = new Uri("https://localhost");

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.ClientCredentials,
                ["scope"] = "identity.api"
            })
        };
        tokenRequest.Headers.Authorization = CreateBasicAuth("test-client", "test-secret");

        using var tokenResponse = await tokenClient.SendAsync(tokenRequest);
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        tokenPayload.ShouldNotBeNull();
        var accessToken = tokenPayload!.RootElement.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNull();

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var startResponse = await client.GetAsync("/auth/external/google/start?mode=link&returnUrl=/link/result");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExternalUnlink_AllowsBearerAuthentication()
    {
        const string email = "unlink-bearer@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();

            var addLoginResult = await userManager.AddLoginAsync(
                user!,
                new UserLoginInfo(IdentityApiFactory.FakeGoogleScheme, "unlink-bearer-key", "Google"));
            addLoginResult.Succeeded.ShouldBeTrue();
        }

        var accessToken = await _factory.CreateAccessTokenAsync(email, password, _factory, "openid profile email offline_access identity.api");

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.BaseAddress = new Uri("https://localhost");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            var logins = await userManager.GetLoginsAsync(user!);
            logins.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ExternalUnlink_RejectsLastSignInMethod_ForExternalOnlyAccount()
    {
        const string email = "external-only@example.com";

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync($"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=External%20Only");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callbackLocation = startResponse.Headers.Location;
        callbackLocation.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(callbackLocation);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var unlinkResponse = await client.DeleteAsync("/auth/external/google");
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await unlinkResponse.Content.ReadAsStringAsync();
        body.ShouldContain("Cannot unlink the last sign-in method");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();
        var logins = await userManager.GetLoginsAsync(user!);
        logins.Count(login => login.LoginProvider == IdentityApiFactory.FakeGoogleScheme).ShouldBe(1);
        (await userManager.HasPasswordAsync(user!)).ShouldBeFalse();
    }

    private WebApplicationFactory<Program> CreateExternalClaimPersistingFactory()
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["Authentication:External:PersistedClaimTypes:0"] = ExternalWorkspaceClaimType
                };
                configurationBuilder.AddInMemoryCollection(overrides);
            });
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, CurrentExternalClaimsStartupFilter>());
        });

    private WebApplicationFactory<Program> CreateLifecycleFactory<TListener>(ExternalRegistrationLifecycleProbe probe)
        where TListener : class, IUserLifecycleListener
        => _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(probe);
                services.AddScoped<TListener>();
                services.AddScoped<IUserLifecycleListener>(provider => provider.GetRequiredService<TListener>());
            });
        });

    private static async Task<Dictionary<string, Microsoft.Extensions.Primitives.StringValues>> CompleteExternalLoginAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string providerKey)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress = new Uri("https://localhost");

        var startResponse = await client.GetAsync(
            $"/auth/external/google/start?returnUrl=/client/callback&email={Uri.EscapeDataString(email)}&name=External%20Lifecycle&key={Uri.EscapeDataString(providerKey)}");
        startResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        startResponse.Headers.Location.ShouldNotBeNull();

        var callbackResponse = await client.GetAsync(startResponse.Headers.Location);
        callbackResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callbackResponse.Headers.Location.ShouldNotBeNull();

        var uri = new Uri(client.BaseAddress, callbackResponse.Headers.Location);
        return QueryHelpers.ParseQuery(uri.Query);
    }

    private sealed class CurrentExternalClaimsStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => application =>
            {
                application.Use(async (context, pipelineNext) =>
                {
                    if (context.Request.Path != "/test/current-external-claims")
                    {
                        await pipelineNext();
                        return;
                    }

                    var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
                    if (!authentication.Succeeded || authentication.Principal is null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }

                    await context.Response.WriteAsJsonAsync(new
                    {
                        externalClaims = authentication.Principal
                            .FindAll(ExternalWorkspaceClaimType)
                            .Select(claim => claim.Value)
                            .ToArray(),
                        authenticationMethod = authentication.Principal
                            .FindFirstValue(ClaimTypes.AuthenticationMethod)
                    });
                });

                next(application);
            };
    }

    private sealed class ExternalRegistrationLifecycleProbe
    {
        public int BeforeRegistrationCalls { get; set; }
        public int AfterRegistrationCalls { get; set; }
        public UserLifecycleContext? LastContext { get; set; }
    }

    private sealed class RecordingExternalRegistrationListener(ExternalRegistrationLifecycleProbe probe) : IUserLifecycleListener
    {
        public ValueTask<LifecycleHookResult> BeforeUserRegisteredAsync(
            UserLifecycleContext context,
            CancellationToken cancellationToken = default)
        {
            probe.BeforeRegistrationCalls++;
            probe.LastContext = context;
            return ValueTask.FromResult(LifecycleHookResult.Continue());
        }

        public ValueTask AfterUserRegisteredAsync(
            UserLifecycleContext context,
            CancellationToken cancellationToken = default)
        {
            probe.AfterRegistrationCalls++;
            probe.LastContext = context;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RejectingExternalRegistrationListener(ExternalRegistrationLifecycleProbe probe) : IUserLifecycleListener
    {
        public ValueTask<LifecycleHookResult> BeforeUserRegisteredAsync(
            UserLifecycleContext context,
            CancellationToken cancellationToken = default)
        {
            probe.BeforeRegistrationCalls++;
            return ValueTask.FromResult(LifecycleHookResult.Fail("External registration disabled."));
        }

        public ValueTask AfterUserRegisteredAsync(
            UserLifecycleContext context,
            CancellationToken cancellationToken = default)
        {
            probe.AfterRegistrationCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ConflictingExternalLoginRegistrationListener(
        ExternalRegistrationLifecycleProbe probe,
        UserManager<ApplicationUser> userManager) : IUserLifecycleListener
    {
        public async ValueTask<LifecycleHookResult> BeforeUserRegisteredAsync(
            UserLifecycleContext context,
            CancellationToken cancellationToken = default)
        {
            probe.BeforeRegistrationCalls++;
            var blockerEmail = $"external-login-conflict-{Guid.NewGuid():N}@example.com";
            var blocker = new ApplicationUser
            {
                Email = blockerEmail,
                UserName = blockerEmail,
                EmailConfirmed = true,
                DisplayName = "External Login Conflict"
            };
            var createResult = await userManager.CreateAsync(blocker);
            createResult.Succeeded.ShouldBeTrue();

            var provider = context.Items!["Provider"].ShouldBeOfType<string>();
            var providerKey = context.Items["ProviderKey"].ShouldBeOfType<string>();
            var addLoginResult = await userManager.AddLoginAsync(
                blocker,
                new UserLoginInfo(provider, providerKey, provider));
            addLoginResult.Succeeded.ShouldBeTrue();

            return LifecycleHookResult.Continue();
        }

        public ValueTask AfterUserRegisteredAsync(
            UserLifecycleContext context,
            CancellationToken cancellationToken = default)
        {
            probe.AfterRegistrationCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private async Task SeedUserAsync(string email, string password, WebApplicationFactory<Program>? factory = null)
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "External Test User"
            };

            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.ShouldBeTrue();
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password, WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? _factory).CreateClient(new WebApplicationFactoryClientOptions
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

        var body = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.IsSuccessStatusCode.ShouldBeTrue(body);
        return client;
    }

    private static AuthenticationHeaderValue CreateBasicAuth(string clientId, string clientSecret)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }
}
