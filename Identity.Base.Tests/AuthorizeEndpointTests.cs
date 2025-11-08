using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Shouldly;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Infrastructure;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles.Services;
using Identity.Base.Roles.Configuration;
using Identity.Base.Tests.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Tests;

public class AuthorizeEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AuthorizeEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Authorize_WhenPromptIsNoneAndUserNotAuthenticated_ReturnsLoginRequiredError()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        const string redirectUri = "https://localhost:3000/auth/callback";
        var state = Guid.NewGuid().ToString("N");
        var pkce = PkceData.Create();
        var query = new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile email",
            [OpenIddictConstants.Parameters.Prompt] = OpenIddictConstants.PromptValues.None,
            [OpenIddictConstants.Parameters.State] = state,
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256
        };

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", query);

        using var response = await client.GetAsync(authorizeUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();

        var location = response.Headers.Location!;
        location.AbsoluteUri.ShouldStartWith(redirectUri);

        var callbackQuery = QueryHelpers.ParseQuery(location.Query);
        callbackQuery[OpenIddictConstants.Parameters.Error].ToString().ShouldBe(OpenIddictConstants.Errors.LoginRequired);
        callbackQuery[OpenIddictConstants.Parameters.State].ToString().ShouldBe(state);
        callbackQuery.ShouldContainKey(OpenIddictConstants.Parameters.ErrorDescription);
    }

    [Fact]
    public async Task Authorize_WhenUserNotAuthenticated_Returns401()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        const string redirectUri = "https://localhost:3000/auth/callback";
        var pkce = PkceData.Create();

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
        });

        using var response = await client.GetAsync(authorizeUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("login_required");
    }

    [Fact]
    public async Task Authorize_WhenPromptConsentAndUserAuthenticated_ReturnsAuthorizationCode()
    {
        const string email = "authorize-consent@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var pkce = PkceData.Create();
        var state = Guid.NewGuid().ToString("N");
        const string redirectUri = "https://localhost:3000/auth/callback";

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = state,
            [OpenIddictConstants.Parameters.Prompt] = OpenIddictConstants.PromptValues.Consent
        });

        using var response = await client.GetAsync(authorizeUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();

        var location = response.Headers.Location!;
        var callbackQuery = QueryHelpers.ParseQuery(location.Query);

        callbackQuery.ShouldContainKey(OpenIddictConstants.Parameters.Code);
        callbackQuery[OpenIddictConstants.Parameters.State].ToString().ShouldBe(state);
    }

    [Fact]
    public async Task AuthorizationCodeFlow_AddsPermissionsClaimToAccessToken()
    {
        const string email = "authorize-permissions@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: true);
        await AssignRoleAsync(email, "IdentityAdmin");

        using var client = await CreateAuthenticatedClientAsync(email, password);

        var pkce = PkceData.Create();
        var state = Guid.NewGuid().ToString("N");
        const string redirectUri = "https://localhost:3000/auth/callback";

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile identity.admin identity.api",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = state,
            [OpenIddictConstants.Parameters.Prompt] = OpenIddictConstants.PromptValues.Consent
        });

        using var authorizeResponse = await client.GetAsync(authorizeUrl);
        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        authorizeResponse.Headers.Location.ShouldNotBeNull();

        var location = authorizeResponse.Headers.Location!;
        var query = QueryHelpers.ParseQuery(location.Query);
        var code = query[OpenIddictConstants.Parameters.Code].ToString();
        code.ShouldNotBeNull();

        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.AuthorizationCode,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Code] = code,
            [OpenIddictConstants.Parameters.CodeVerifier] = pkce.CodeVerifier
        });
        using var tokenResponse = await client.PostAsync("/connect/token", tokenRequest);

        var tokenPayload = await tokenResponse.Content.ReadAsStringAsync();
        tokenResponse.IsSuccessStatusCode.ShouldBeTrue(tokenPayload);

        var json = JsonDocument.Parse(tokenPayload);
        var accessToken = json.RootElement.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNull();

        var parts = accessToken!.Split('.');
        parts.Length.ShouldBeGreaterThanOrEqualTo(2);
        var payload = Encoding.UTF8.GetString(JwtTestUtilities.Base64UrlDecode(parts[1]));
        using var tokenJson = JsonDocument.Parse(payload);

        tokenJson.RootElement.TryGetProperty("identity.permissions", out var permissionsProperty).ShouldBeTrue();
        var permissions = permissionsProperty.GetString();
        permissions.ShouldNotBeNull();
        permissions!.ShouldContain("users.create");
        permissions.ShouldContain("roles.manage");
    }

    [Fact]
    public async Task Logout_ClearsSessionAndForcesReauthentication()
    {
        const string email = "authorize-logout@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: true);

        using var client = await CreateAuthenticatedClientAsync(email, password);

        const string redirectUri = "https://localhost:3000/auth/callback";
        var pkce = PkceData.Create();
        var state = Guid.NewGuid().ToString("N");

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = pkce.CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = state
        });

        using (var authorizeResponse = await client.GetAsync(authorizeUrl))
        {
            authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        using (var logoutResponse = await client.PostAsync("/auth/logout", new StringContent(string.Empty)))
        {
            logoutResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var secondAuthorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "spa-client",
            [OpenIddictConstants.Parameters.RedirectUri] = redirectUri,
            [OpenIddictConstants.Parameters.Scope] = "openid profile",
            [OpenIddictConstants.Parameters.CodeChallenge] = PkceData.Create().CodeChallenge,
            [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.State] = Guid.NewGuid().ToString("N")
        });

        using var unauthorizedResponse = await client.GetAsync(secondAuthorizeUrl);
        unauthorizedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task SeedUserAsync(string email, string password, bool confirmEmail, WebApplicationFactory<Program>? factory = null)
    {
        var targetFactory = factory ?? _factory;
        using var scope = targetFactory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = confirmEmail,
                DisplayName = "Authorize Test User"
            };

            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.ShouldBeTrue();
        }
        else if (confirmEmail && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        if (confirmEmail && !await userManager.IsEmailConfirmedAsync(user))
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
    }

    private async Task AssignRoleAsync(string email, string roleName, WebApplicationFactory<Program>? factory = null)
    {
        var targetFactory = factory ?? _factory;
        using var scope = targetFactory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();

        await scope.ServiceProvider.SeedIdentityRolesAsync();

        var assignmentService = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();
        await assignmentService.AssignRolesAsync(user!.Id, new[] { roleName });
    }

    private async Task<string> CreateAccessTokenAsync(string email, string password, WebApplicationFactory<Program>? factory = null, string scope = "identity.api identity.admin openid profile")
    {
        var targetFactory = factory ?? _factory;
        using var client = targetFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.Password,
            [OpenIddictConstants.Parameters.Username] = email,
            [OpenIddictConstants.Parameters.Password] = password,
            [OpenIddictConstants.Parameters.ClientId] = "test-client",
            [OpenIddictConstants.Parameters.ClientSecret] = "test-secret",
            [OpenIddictConstants.Parameters.Scope] = scope
        });

        using var response = await client.PostAsync("/connect/token", tokenRequest);
        var payload = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(payload);

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password, WebApplicationFactory<Program>? factory = null)
    {
        var targetFactory = factory ?? _factory;

        var client = targetFactory.CreateClient(new WebApplicationFactoryClientOptions
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

        var loginPayload = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.IsSuccessStatusCode.ShouldBeTrue(loginPayload);

        return client;
    }
    [Fact]
    public async Task UserPermissions_ReturnsUserRolesOnly_WhenNoOrganizationHeader()
    {
        const string email = "permissions-no-org@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: true);
        await AssignRoleAsync(email, "IdentityAdmin");

        var token = await CreateAccessTokenAsync(email, password);
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/users/me/permissions");
        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, payload);

        var document = JsonDocument.Parse(payload);
        var permissions = document.RootElement.GetProperty("permissions").EnumerateArray().Select(element => element.GetString()).ToList();
        permissions.ShouldContain("users.create");
        permissions.ShouldContain("roles.manage");
        permissions.ShouldNotContain("user.organizations.members.manage");
    }

    [Fact]
    public async Task UserPermissions_IncludesOrganizationPermissions_WhenHeaderPresent()
    {
        const string email = "permissions-with-org@example.com";
        const string password = "StrongPass!2345";
        using var organizationFactory = new OrganizationApiFactory();
        await SeedUserAsync(email, password, confirmEmail: true, organizationFactory);
        await AssignRoleAsync(email, "IdentityAdmin", organizationFactory);

        var organizationId = await CreateOrganizationAsync($"perm-org-{Guid.NewGuid():N}", "Permission Org", organizationFactory);
        await AddMembershipAsync(organizationId, email, organizationFactory);

        var token = await CreateAccessTokenAsync(email, password, organizationFactory);
        using var client = organizationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add(OrganizationContextHeaderNames.OrganizationId, organizationId.ToString("D"));

        var response = await client.GetAsync("/users/me/permissions");
        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, payload);

        var document = JsonDocument.Parse(payload);
        var permissions = document.RootElement.GetProperty("permissions").EnumerateArray().Select(element => element.GetString()).ToList();
        permissions.ShouldContain("users.create");
        permissions.ShouldContain("roles.manage");
        permissions.ShouldContain("user.organizations.members.manage");
    }

    private async Task<Guid> CreateOrganizationAsync(string slug, string displayName, WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
        var organization = await organizationService.CreateAsync(new OrganizationCreateRequest
        {
            Slug = slug,
            DisplayName = displayName
        });
        return organization.Id;
    }

    private async Task AddMembershipAsync(Guid organizationId, string email, WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull();

        var roleSeeder = scope.ServiceProvider.GetRequiredService<OrganizationRoleSeeder>();
        await roleSeeder.SeedAsync();

        var membershipService = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();

        var ownerRoleId = await dbContext.OrganizationRoles
            .AsNoTracking()
            .Where(role => role.OrganizationId == null && role.Name == "OrgOwner")
            .Select(role => role.Id)
            .FirstAsync();

        await membershipService.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organizationId,
            UserId = user!.Id,
            RoleIds = new[] { ownerRoleId }
        });
    }
}
