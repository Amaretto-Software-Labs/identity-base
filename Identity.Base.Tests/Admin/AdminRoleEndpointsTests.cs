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
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Identity.Base.Tests.Admin;

[Collection("AdminEndpoints")]
public class AdminRoleEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AdminRoleEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListRoles_ReturnsSeededRoles()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("roles-list-admin@example.com", "AdminPass!2345");

        using var client = CreateAuthorizedClient(token);
        var response = await client.GetAsync("/admin/roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var roles = await response.Content.ReadFromJsonAsync<List<AdminRoleSummaryDto>>(JsonOptions);
        roles.Should().NotBeNull();
        roles!.Should().Contain(role => role.Name == "IdentityAdmin");
    }

    [Fact]
    public async Task CreateRole_CreatesRoleWithPermissions()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("roles-create-admin@example.com", "AdminPass!2345");
        using var client = CreateAuthorizedClient(token);

        var response = await client.PostAsJsonAsync("/admin/roles", new
        {
            Name = "SupportSupervisor",
            Description = "Supervises support agents",
            Permissions = new[] { "users.read", "users.update" }
        }, JsonOptions);

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, payload);

        var created = await response.Content.ReadFromJsonAsync<AdminRoleDetailDto>(JsonOptions);
        created.Should().NotBeNull();
        created!.Permissions.Should().BeEquivalentTo(new[] { "users.read", "users.update" });

        using var scope = _factory.Services.CreateScope();
        var roleDb = scope.ServiceProvider.GetRequiredService<IRoleDbContext>();
        var role = await roleDb.Roles.FirstOrDefaultAsync(r => r.Name == "SupportSupervisor");
        role.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateRole_DetectsConcurrencyConflicts()
    {
        var (roleId, _, concurrencyStamp, token) = await CreateRoleAsync("roles-update-admin@example.com", "AdminPass!2345");
        using var client = CreateAuthorizedClient(token);

        var badRequest = new
        {
            ConcurrencyStamp = "bad-stamp",
            Permissions = new[] { "users.read" }
        };

        var response = await client.PutAsJsonAsync($"/admin/roles/{roleId:D}", badRequest, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updateResponse = await client.PutAsJsonAsync($"/admin/roles/{roleId:D}", new
        {
            ConcurrencyStamp = concurrencyStamp,
            Description = "Updated description",
            Permissions = new[] { "users.read", "users.update" }
        }, JsonOptions);

        var payload = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        var updated = await updateResponse.Content.ReadFromJsonAsync<AdminRoleDetailDto>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteRole_FailsForSystemRole()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("roles-delete-system@example.com", "AdminPass!2345");
        using var client = CreateAuthorizedClient(token);

        var list = await client.GetFromJsonAsync<List<AdminRoleSummaryDto>>("/admin/roles", JsonOptions);
        var systemRole = list!.First(role => role.Name == "IdentityAdmin");

        var response = await client.DeleteAsync($"/admin/roles/{systemRole.Id:D}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteRole_FailsWhenAssignmentsExist()
    {
        var (roleId, roleName, _, token) = await CreateRoleAsync("roles-delete-assigned@example.com", "AdminPass!2345", new[] { "users.read" });

        using (var scope = _factory.Services.CreateScope())
        {
            var assignmentService = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Email = "role-assigned-user@example.com", UserName = "role-assigned-user@example.com", EmailConfirmed = true };
            await userManager.CreateAsync(user, "Str0ngP@ssword!");
            await assignmentService.AssignRolesAsync(user.Id, new[] { roleName });
        }

        using var client = CreateAuthorizedClient(token);
        var response = await client.DeleteAsync($"/admin/roles/{roleId:D}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteRole_SucceedsWhenUnused()
    {
        var (roleId, _, _, token) = await CreateRoleAsync("roles-delete-unused@example.com", "AdminPass!2345", new[] { "users.read" });
        using var client = CreateAuthorizedClient(token);

        var response = await client.DeleteAsync($"/admin/roles/{roleId:D}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<(Guid RoleId, string RoleName, string ConcurrencyStamp, string AccessToken)> CreateRoleAsync(string adminEmail, string adminPassword, IEnumerable<string>? permissions = null)
    {
        var (adminId, token) = await CreateAdminUserAndTokenAsync(adminEmail, adminPassword);
        using var client = CreateAuthorizedClient(token);

        var roleName = $"TempRole-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/admin/roles", new
        {
            Name = roleName,
            Description = "Temporary role",
            Permissions = permissions ?? new[] { "users.read" }
        }, JsonOptions);

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, payload);
        var detail = await response.Content.ReadFromJsonAsync<AdminRoleDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        return (detail!.Id, roleName, detail.ConcurrencyStamp, token);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleAssignment = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Admin"
            };

            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.Should().BeTrue(result.Errors.FirstOrDefault()?.Description);
            await roleAssignment.AssignRolesAsync(user.Id, new[] { "IdentityAdmin" });
            existing = user;
        }
        else
        {
            await roleAssignment.AssignRolesAsync(existing.Id, new[] { "IdentityAdmin" });
        }

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
            [OpenIddictConstants.Parameters.Scope] = "openid profile email identity.api identity.admin"
        }));

        var payloadJson = await tokenResponse.Content.ReadAsStringAsync();
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK, payloadJson);
        var tokenPayload = JsonDocument.Parse(payloadJson);
        var accessToken = tokenPayload.RootElement.GetProperty("access_token").GetString();
        return (existing!.Id, accessToken!);
    }

    private HttpClient CreateAuthorizedClient(string token)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record AdminRoleSummaryDto(Guid Id, string Name, string? Description, bool IsSystemRole, string ConcurrencyStamp, List<string> Permissions, int UserCount);

    private sealed record AdminRoleDetailDto(Guid Id, string Name, string? Description, bool IsSystemRole, string ConcurrencyStamp, List<string> Permissions);
}
