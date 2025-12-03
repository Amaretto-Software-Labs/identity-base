using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Services;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Identity.Base.Tests.Organizations;

namespace Identity.Base.Tests;

public class RefreshTokenAugmentorTests : IClassFixture<OrganizationApiFactory>
{
    private readonly OrganizationApiFactory _factory;

    public RefreshTokenAugmentorTests(OrganizationApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RefreshToken_ReappliesMembershipAugmentor_ToIncludeNewOrganization()
    {
        // Arrange: seed user and obtain tokens
        const string email = "refresh-aug@example.com";
        const string password = "StrongPass!2345";

        await SeedUserAsync(email, password);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress ??= new Uri("https://localhost");

        var token = await PasswordTokenAsync(client, email, password, scope: "openid profile email offline_access");
        token.AccessToken.ShouldNotBeNull();
        token.RefreshToken.ShouldNotBeNull();

        // Act: create organization membership AFTER token issuance
        Guid userId;
        Guid createdOrgId = Guid.Empty;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();
            userId = user!.Id;

            var orgDb = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var orgService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();
            var membershipService = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipService>();

            var org = await orgService.CreateAsync(new OrganizationCreateRequest
            {
                Slug = $"test-{Guid.NewGuid():N}".Substring(0, 12),
                DisplayName = $"Test Org {Guid.NewGuid():N}"
            });
            createdOrgId = org.Id;

            // Assign default owner role so user has user.organizations.* permissions
            var ownerRoleName = scope.ServiceProvider.GetRequiredService<IOptions<OrganizationRoleOptions>>()
                .Value.OwnerRoleName;
            var ownerRoleId = await orgDb.OrganizationRoles
                .AsNoTracking()
                .Where(r => r.OrganizationId == Guid.Empty && r.Name == ownerRoleName)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();
            ownerRoleId.ShouldNotBe(Guid.Empty);

            await membershipService.AddMemberAsync(new OrganizationMembershipRequest
            {
                OrganizationId = org.Id,
                UserId = userId,
                RoleIds = new[] { ownerRoleId }
            });

            await orgDb.SaveChangesAsync();
        }

        // Assert: user-scoped org endpoint succeeds without X-Organization-Id using the refreshed token
        var refreshed = await RefreshTokenAsync(client, token.RefreshToken!);
        refreshed.AccessToken.ShouldNotBeNull();
        refreshed.RefreshToken.ShouldNotBeNull();
        using var orgReq = new HttpRequestMessage(HttpMethod.Get, $"/users/me/organizations/{createdOrgId}");
        orgReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        using var orgRes = await client.SendAsync(orgReq);
        orgRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var orgJson = await orgRes.Content.ReadFromJsonAsync<JsonDocument>();
        orgJson.ShouldNotBeNull();
        var returnedId = orgJson!.RootElement.GetProperty("id").GetString();
        returnedId.ShouldBe(createdOrgId.ToString("D"));
    }

    [Fact]
    public async Task PermissionsEndpoint_WithHeader_BeforeRefresh_ReturnsForbidden()
    {
        // Arrange: seed user and obtain initial tokens
        const string email = "header-before-refresh@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.BaseAddress ??= new Uri("https://localhost");

        var token = await PasswordTokenAsync(client, email, password, scope: "openid profile email offline_access");
        token.AccessToken.ShouldNotBeNull();

        Guid createdOrgId = Guid.Empty;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            user.ShouldNotBeNull();

            var orgDb = sp.GetRequiredService<OrganizationDbContext>();
            var orgService = sp.GetRequiredService<IOrganizationService>();
            var membershipService = sp.GetRequiredService<IOrganizationMembershipService>();
            var ownerRoleName = sp.GetRequiredService<IOptions<OrganizationRoleOptions>>().Value.OwnerRoleName;

            var org = await orgService.CreateAsync(new OrganizationCreateRequest
            {
                Slug = $"test-{Guid.NewGuid():N}".Substring(0, 12),
                DisplayName = "Test Org"
            });
            createdOrgId = org.Id;

            var ownerRoleId = await orgDb.OrganizationRoles
                .AsNoTracking()
                .Where(r => r.OrganizationId == Guid.Empty && r.Name == ownerRoleName)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();
            ownerRoleId.ShouldNotBe(Guid.Empty);

            await membershipService.AddMemberAsync(new OrganizationMembershipRequest
            {
                OrganizationId = org.Id,
                UserId = user!.Id,
                RoleIds = new[] { ownerRoleId }
            });

            await orgDb.SaveChangesAsync();
        }

        // Act: Call /users/me/permissions with the X-Organization-Id header using the original (pre-refresh) token
        using var me = new HttpRequestMessage(HttpMethod.Get, "/users/me/permissions");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        me.Headers.Add("X-Organization-Id", createdOrgId.ToString("D"));

        using var meResponse = await client.SendAsync(me);

        // Assert: middleware denies due to missing org:memberships claim in the original token
        meResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }


    private static async Task<(string? AccessToken, string? RefreshToken)> PasswordTokenAsync(HttpClient client, string email, string password, string scope)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = email,
                ["password"] = password,
                ["scope"] = scope
            })
        };
        request.Headers.Authorization = CreateBasicAuth("test-client", "test-secret");

        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json.ShouldNotBeNull();
        var access = json!.RootElement.GetProperty("access_token").GetString();
        var refresh = json!.RootElement.GetProperty("refresh_token").GetString();
        return (access, refresh);
    }

    private static async Task<(string? AccessToken, string? RefreshToken)> RefreshTokenAsync(HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            })
        };
        // Refresh with the same confidential client used for password grant
        request.Headers.Authorization = CreateBasicAuth("test-client", "test-secret");

        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        json.ShouldNotBeNull();
        var access = json!.RootElement.GetProperty("access_token").GetString();
        var refresh = json!.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        return (access, refresh);
    }

    private async Task SeedUserAsync(string email, string password)
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
                EmailConfirmed = true,
                DisplayName = "Refresh Flow User"
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

    private static AuthenticationHeaderValue CreateBasicAuth(string clientId, string clientSecret)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    // No JWT parsing needed for behavior test
}
