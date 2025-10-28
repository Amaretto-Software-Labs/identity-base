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
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);

        var payload = JsonSerializer.Deserialize<AdminRoleListResponseDto>(responseBody, JsonOptions);
        payload.Should().NotBeNull(responseBody);
        payload!.Roles.Should().Contain(role => role.Name == "IdentityAdmin");
    }

    [Fact]
    public async Task ListRoles_CapsPageSize_AndAppliesSorting()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("roles-paging-admin@example.com", "AdminPass!2345");

        List<string> expectedNames;
        int expectedTotal;
        using (var scope = _factory.Services.CreateScope())
        {
            var roleDb = scope.ServiceProvider.GetRequiredService<IRoleDbContext>();
            await SeedRolesAsync(roleDb, prefix: "paging-role", count: 210);

            expectedTotal = await roleDb.Roles.CountAsync();
            expectedNames = await roleDb.Roles
                .AsNoTracking()
                .OrderByDescending(role => role.Name)
                .ThenBy(role => role.Id)
                .Select(role => role.Name)
                .Skip(200)
                .Take(200)
                .ToListAsync();
        }

        using var client = CreateAuthorizedClient(token);
        var response = await client.GetAsync("/admin/roles?page=2&pageSize=500&sort=name:desc");
        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);

        var payload = JsonSerializer.Deserialize<AdminRoleListResponseDto>(responseBody, JsonOptions);
        payload.Should().NotBeNull();
        payload!.Page.Should().Be(2);
        payload.PageSize.Should().Be(200);
        payload.TotalCount.Should().Be(expectedTotal);
        payload.Roles.Should().HaveCount(expectedNames.Count);
        payload.Roles.Select(role => role.Name)
            .Should().BeEquivalentTo(expectedNames, options => options.WithStrictOrdering());
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

        var listResponse = await client.GetAsync("/admin/roles");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        var list = JsonSerializer.Deserialize<AdminRoleListResponseDto>(listBody, JsonOptions);
        list.Should().NotBeNull(listBody);
        var systemRole = list!.Roles.First(role => role.Name == "IdentityAdmin");

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

    private static async Task SeedRolesAsync(IRoleDbContext context, string prefix, int count)
    {
        var existing = await context.Roles
            .Where(role => role.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToListAsync();

        if (existing.Count > 0)
        {
            var existingIds = existing.Select(role => role.Id).ToList();

            var assignments = await context.UserRoles
                .Where(userRole => existingIds.Contains(userRole.RoleId))
                .ToListAsync();
            if (assignments.Count > 0)
            {
                context.UserRoles.RemoveRange(assignments);
            }

            var rolePermissions = await context.RolePermissions
                .Where(permission => existingIds.Contains(permission.RoleId))
                .ToListAsync();
            if (rolePermissions.Count > 0)
            {
                context.RolePermissions.RemoveRange(rolePermissions);
            }

            context.Roles.RemoveRange(existing);
            await context.SaveChangesAsync();
        }

        for (var index = 0; index < count; index++)
        {
            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = $"{prefix}-{index:000}",
                Description = "Paging role sample",
                IsSystemRole = false,
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };

            context.Roles.Add(role);
        }

        await context.SaveChangesAsync();
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

    private sealed record AdminRoleListResponseDto(int Page, int PageSize, int TotalCount, List<AdminRoleSummaryDto> Roles);
}
