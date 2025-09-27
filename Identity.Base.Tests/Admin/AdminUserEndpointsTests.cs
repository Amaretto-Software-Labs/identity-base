using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Base.Identity;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Services;
using Identity.Base.Tests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Identity.Base.Tests.Admin;

public class AdminUserEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AdminUserEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListUsers_ReturnsAdminUser_WhenCallerHasPermission()
    {
        const string email = "admin-list@example.com";
        const string password = "AdminPass!2345";

        var (_, token) = await CreateAdminUserAndTokenAsync(email, password, includeAdminScope: true);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/admin/users?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminUserListDto>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.TotalCount.Should().BeGreaterThan(0);
        payload.Users.Should().Contain(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        var adminEntry = payload.Users.First(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        adminEntry.Roles.Should().Contain("IdentityAdmin");
    }

    [Fact]
    public async Task GetUser_ReturnsDetail_WhenCallerHasPermission()
    {
        const string email = "admin-detail@example.com";
        const string password = "AdminPass!2345";

        var (userId, token) = await CreateAdminUserAndTokenAsync(email, password, includeAdminScope: true);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/admin/users/{userId:D}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Email.Should().Be(email);
        payload.Roles.Should().Contain("IdentityAdmin");
        payload.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task ListUsers_ReturnsForbidden_WhenScopeMissing()
    {
        const string email = "admin-noscope@example.com";
        const string password = "AdminPass!2345";

        var (_, token) = await CreateAdminUserAndTokenAsync(email, password, includeAdminScope: false);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(string email, string password, bool includeAdminScope)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleAssignmentService = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            await roleAssignmentService.AssignRolesAsync(existing.Id, new[] { "IdentityAdmin" });
        }
        else
        {
            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Admin Tester"
            };

            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.Should().BeTrue(createResult.Errors.FirstOrDefault()?.Description);

            await roleAssignmentService.AssignRolesAsync(user.Id, new[] { "IdentityAdmin" });
            existing = user;
        }

        var scopeValue = includeAdminScope
            ? string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api", "identity.admin" })
            : string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api" });

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.Password,
            [OpenIddictConstants.Parameters.Username] = email,
            [OpenIddictConstants.Parameters.Password] = password,
            [OpenIddictConstants.Parameters.ClientId] = "test-client",
            [OpenIddictConstants.Parameters.ClientSecret] = "test-secret",
            [OpenIddictConstants.Parameters.Scope] = scopeValue
        }));

        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK, tokenPayload?.RootElement.ToString());
        tokenPayload.Should().NotBeNull();

        var accessToken = tokenPayload!.RootElement.GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();

        return (existing!.Id, accessToken!);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record AdminUserListDto(int Page, int PageSize, int TotalCount, List<AdminUserSummaryDto> Users);

    private sealed record AdminUserSummaryDto(Guid Id, string? Email, string? DisplayName, bool EmailConfirmed, bool IsLockedOut, DateTimeOffset CreatedAt, bool MfaEnabled, List<string> Roles);

    private sealed record AdminUserDetailDto(Guid Id, string? Email, bool EmailConfirmed, string? DisplayName, DateTimeOffset CreatedAt, bool LockoutEnabled, bool IsLockedOut, DateTimeOffset? LockoutEnd, bool TwoFactorEnabled, bool PhoneNumberConfirmed, string? PhoneNumber, Dictionary<string, string?> Metadata, string ConcurrencyStamp, List<string> Roles, List<AdminUserExternalLoginDto> ExternalLogins, bool AuthenticatorConfigured);

    private sealed record AdminUserExternalLoginDto(string Provider, string DisplayName, string Key);
}
