using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Identity.Base.Tests.Admin;

[Collection("AdminEndpoints")]
public sealed class AdminPermissionEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AdminPermissionEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListPermissions_returns_forbidden_when_scope_missing()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("permissions-noscope@example.com", "AdminPass!2345", includeAdminScope: false);
        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/permissions");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListPermissions_returns_paged_results_and_usage_counts()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("permissions-list@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/permissions?page=1&pageSize=25");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminPermissionListResponseDto>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.TotalCount.ShouldBeGreaterThan(0);
        payload.Items.Count.ShouldBeGreaterThan(0);

        var rolesRead = payload.Items.FirstOrDefault(item => item.Name == "roles.read");
        rolesRead.ShouldNotBeNull();
        rolesRead!.RoleCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ListPermissions_returns_empty_when_search_has_no_match()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("permissions-empty@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/permissions?page=1&pageSize=25&search=definitely-not-a-permission");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AdminPermissionListResponseDto>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.TotalCount.ShouldBe(0);
        payload.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListPermissions_caps_page_size_and_applies_sorting()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("permissions-paging@example.com", "AdminPass!2345", includeAdminScope: true);

        List<string> expectedNames;
        int expectedTotal;
        using (var scope = _factory.Services.CreateScope())
        {
            var roleDb = scope.ServiceProvider.GetRequiredService<IRoleDbContext>();
            await SeedPermissionsAsync(roleDb, prefix: "paging-permission", count: 250);

            expectedTotal = await roleDb.Permissions.CountAsync();
            expectedNames = await roleDb.Permissions
                .AsNoTracking()
                .OrderByDescending(permission => permission.Name)
                .ThenBy(permission => permission.Id)
                .Select(permission => permission.Name)
                .Skip(200)
                .Take(200)
                .ToListAsync();
        }

        using var client = CreateAuthorizedClient(token);
        var response = await client.GetAsync("/admin/permissions?page=2&pageSize=500&sort=name:desc");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = JsonSerializer.Deserialize<AdminPermissionListResponseDto>(body, JsonOptions);
        payload.ShouldNotBeNull();
        payload!.Page.ShouldBe(2);
        payload.PageSize.ShouldBe(200);
        payload.TotalCount.ShouldBe(expectedTotal);
        payload.Items.Select(item => item.Name).ToList().ShouldBe(expectedNames);
    }

    [Fact]
    public async Task ListPermissions_supports_usage_sort()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("permissions-usage@example.com", "AdminPass!2345", includeAdminScope: true);
        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/permissions?page=1&pageSize=25&sort=usage:desc&search=roles");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = JsonSerializer.Deserialize<AdminPermissionListResponseDto>(body, JsonOptions);
        payload.ShouldNotBeNull();
        payload!.Items.Count.ShouldBeGreaterThan(0);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(string email, string password, bool includeAdminScope)
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
            result.Succeeded.ShouldBeTrue(result.Errors.FirstOrDefault()?.Description);
            await roleAssignment.AssignRolesAsync(user.Id, new[] { "IdentityAdmin" });
            existing = user;
        }
        else
        {
            await roleAssignment.AssignRolesAsync(existing.Id, new[] { "IdentityAdmin" });
        }

        var scopeString = includeAdminScope
            ? "openid profile email identity.api identity.admin"
            : "openid profile email identity.api";
        var accessToken = await _factory.CreateAccessTokenAsync(email, password, scope: scopeString);
        accessToken.ShouldNotBeNullOrWhiteSpace();

        return (existing!.Id, accessToken);
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

    private static async Task SeedPermissionsAsync(IRoleDbContext context, string prefix, int count)
    {
        var existing = await context.Permissions
            .Where(permission => permission.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToListAsync();
        if (existing.Count > 0)
        {
            context.Permissions.RemoveRange(existing);
            await context.SaveChangesAsync();
        }

        for (var index = 0; index < count; index++)
        {
            context.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Name = $"{prefix}-{index:000}",
                Description = "Paging permission sample"
            });
        }

        await context.SaveChangesAsync();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record AdminPermissionSummaryDto(Guid Id, string Name, string? Description, int RoleCount);

    private sealed record AdminPermissionListResponseDto(int Page, int PageSize, int TotalCount, List<AdminPermissionSummaryDto> Items);
}

