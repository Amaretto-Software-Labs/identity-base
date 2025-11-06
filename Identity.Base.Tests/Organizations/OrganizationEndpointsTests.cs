using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Api.Models;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Extensions;
using Identity.Base.Organizations.Infrastructure;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Shouldly;

namespace Identity.Base.Tests.Organizations;

[Collection("AdminEndpoints")]
public class OrganizationEndpointsTests : IClassFixture<OrganizationApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrganizationApiFactory _factory;

    public OrganizationEndpointsTests(OrganizationApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_Can_List_Organizations_With_Or_Without_Header()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-orgs@example.com", "AdminPass!2345", includeAdminScope: true);

        var organizationId = await CreateOrganizationAsync($"org-test-{Guid.NewGuid():N}", "Test Organization");

        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/organizations");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<List<OrganizationDto>>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.ShouldContain(item => item.Id == organizationId);

        client.DefaultRequestHeaders.Add(OrganizationContextHeaderNames.OrganizationId, Guid.NewGuid().ToString("D"));

        var headerResponse = await client.GetAsync("/admin/organizations");
        headerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_Can_Create_Organization_Without_Header()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-create-org@example.com", "AdminPass!2345", includeAdminScope: true);

        using var client = CreateAuthorizedClient(token);

        var request = new
        {
            Slug = $"admin-create-{Guid.NewGuid():N}",
            DisplayName = "Admin Created Org",
            Metadata = new Dictionary<string, string?>()
        };

        var response = await client.PostAsJsonAsync("/admin/organizations", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<OrganizationDto>(JsonOptions);
        dto.ShouldNotBeNull();
        dto!.Slug.ShouldBe(request.Slug);
    }

    [Fact]
    public async Task NonAdmin_Cannot_Access_Admin_Endpoints()
    {
        var (_, token) = await CreateStandardUserAndTokenAsync("standard-user@example.com", "UserPass!2345");

        using var client = CreateAuthorizedClient(token);

        var listResponse = await client.GetAsync("/admin/organizations");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var createResponse = await client.PostAsJsonAsync("/admin/organizations", new
        {
            Slug = $"non-admin-{Guid.NewGuid():N}",
            DisplayName = "Non Admin Org",
            Metadata = new Dictionary<string, string?>()
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_Endpoints_Return_Memberships_And_Support_Header()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-{Guid.NewGuid():N}", "User Org");
        var (userId, _) = await CreateStandardUserAndTokenAsync("org-member@example.com", "UserPass!2345");

        await AddMembershipAsync(organizationId, userId, isPrimary: true);

        var (_, refreshedToken) = await CreateStandardUserAndTokenAsync("org-member@example.com", "UserPass!2345");

        using var client = CreateAuthorizedClient(refreshedToken);

        var memberships = await client.GetFromJsonAsync<PagedResult<UserOrganizationMembershipDto>>("/users/me/organizations", JsonOptions);
        memberships.ShouldNotBeNull();
        memberships!.Items.ShouldContain(m => m.OrganizationId == organizationId);

        client.DefaultRequestHeaders.Add(OrganizationContextHeaderNames.OrganizationId, organizationId.ToString("D"));

        var secondResponse = await client.GetAsync("/users/me/organizations");
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_Organizations_List_AppliesPagingSortingAndSearch()
    {
        var (userId, token) = await CreateStandardUserAndTokenAsync("org-member@example.com", "UserPass!2345");

        var primaryOrgId = await CreateOrganizationAsync($"primary-org-{Guid.NewGuid():N}", "Primary Org");
        var secondaryOrgId = await CreateOrganizationAsync($"secondary-org-{Guid.NewGuid():N}", "Secondary Org");
        var tertiaryOrgId = await CreateOrganizationAsync($"tertiary-org-{Guid.NewGuid():N}", "Tertiary Org");

        await AddMembershipAsync(primaryOrgId, userId, isPrimary: true);
        await AddMembershipAsync(secondaryOrgId, userId, isPrimary: false);
        await AddMembershipAsync(tertiaryOrgId, userId, isPrimary: false);

        using var client = CreateAuthorizedClient(token);

        var page1Response = await client.GetAsync("/users/me/organizations?page=1&pageSize=2&sort=slug:asc");
        var page1Content = await page1Response.Content.ReadAsStringAsync();
        page1Response.StatusCode.ShouldBe(HttpStatusCode.OK, page1Content);
        var page1 = JsonSerializer.Deserialize<PagedResult<UserOrganizationMembershipDto>>(page1Content, JsonOptions);
        page1.ShouldNotBeNull();
        page1!.Items.Count.ShouldBe(2);

        var page2Response = await client.GetAsync("/users/me/organizations?page=2&pageSize=2&sort=slug:asc");
        var page2Content = await page2Response.Content.ReadAsStringAsync();
        page2Response.StatusCode.ShouldBe(HttpStatusCode.OK, page2Content);
        var page2 = JsonSerializer.Deserialize<PagedResult<UserOrganizationMembershipDto>>(page2Content, JsonOptions);
        page2.ShouldNotBeNull();
        page2!.Items.Count.ShouldBeGreaterThanOrEqualTo(1);

        var searchResponse = await client.GetAsync("/users/me/organizations?search=secondary");
        var searchContent = await searchResponse.Content.ReadAsStringAsync();
        searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, searchContent);
        var search = JsonSerializer.Deserialize<PagedResult<UserOrganizationMembershipDto>>(searchContent, JsonOptions);
        search.ShouldNotBeNull();
        search!.Items.ShouldContain(m => m.OrganizationId == secondaryOrgId);
        search.Items.ShouldNotContain(m => m.OrganizationId == primaryOrgId);

        // Archive tertiary org, ensure includeArchived flag controls visibility.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var org = await db.Organizations.FindAsync(tertiaryOrgId);
            org!.Status = OrganizationStatus.Archived;
            await db.SaveChangesAsync();
        }

        var withoutArchivedResponse = await client.GetAsync("/users/me/organizations");
        var withoutArchivedContent = await withoutArchivedResponse.Content.ReadAsStringAsync();
        withoutArchivedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, withoutArchivedContent);
        var withoutArchived = JsonSerializer.Deserialize<PagedResult<UserOrganizationMembershipDto>>(withoutArchivedContent, JsonOptions);
        withoutArchived.ShouldNotBeNull();
        withoutArchived!.Items.ShouldNotContain(m => m.OrganizationId == tertiaryOrgId);

        var withArchivedResponse = await client.GetAsync("/users/me/organizations?includeArchived=true");
        var withArchivedContent = await withArchivedResponse.Content.ReadAsStringAsync();
        withArchivedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, withArchivedContent);
        var withArchived = JsonSerializer.Deserialize<PagedResult<UserOrganizationMembershipDto>>(withArchivedContent, JsonOptions);
        withArchived.ShouldNotBeNull();
        withArchived!.Items.ShouldContain(m => m.OrganizationId == tertiaryOrgId);
    }

    [Fact]
    public async Task Admin_Can_Update_And_Delete_Organization()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-update-org@example.com", "AdminPass!2345", includeAdminScope: true);
        var organizationId = await CreateOrganizationAsync($"org-update-{Guid.NewGuid():N}", "Update Target Org");

        using var client = CreateAuthorizedClient(token);

        var patchResponse = await client.PatchAsJsonAsync($"/admin/organizations/{organizationId}", new
        {
            DisplayName = "Updated Organization Name"
        });

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patched = await patchResponse.Content.ReadFromJsonAsync<OrganizationDto>(JsonOptions);
        patched.ShouldNotBeNull();
        patched!.DisplayName.ShouldBe("Updated Organization Name");
        patched.Status.ShouldBe(OrganizationStatus.Active);

        var deleteResponse = await client.DeleteAsync($"/admin/organizations/{organizationId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/admin/organizations/{organizationId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var archived = await getResponse.Content.ReadFromJsonAsync<OrganizationDto>(JsonOptions);
        archived.ShouldNotBeNull();
        archived!.Status.ShouldBe(OrganizationStatus.Archived);
    }

    [Fact]
    public async Task Admin_Can_Add_And_Remove_Organization_Members()
    {
        var (adminId, token) = await CreateAdminUserAndTokenAsync("admin-memberships@example.com", "AdminPass!2345", includeAdminScope: true);
        var organizationId = await CreateOrganizationAsync($"org-members-{Guid.NewGuid():N}", "Membership Org");
        await AddMembershipAsync(organizationId, adminId, isPrimary: true);

        var (memberId, _) = await CreateStandardUserAndTokenAsync("member-user@example.com", "UserPass!2345");

        using var client = CreateAuthorizedClient(token);

        var addResponse = await client.PostAsJsonAsync($"/admin/organizations/{organizationId}/members", new
        {
            UserId = memberId,
            IsPrimary = false,
            RoleIds = Array.Empty<Guid>()
        });

        addResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await addResponse.Content.ReadFromJsonAsync<OrganizationMembershipDto>(JsonOptions);
        created.ShouldNotBeNull();
        created!.UserId.ShouldBe(memberId);
        created.RoleIds.ShouldBeEmpty();

        var listResponse = await client.GetAsync($"/admin/organizations/{organizationId}/members");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<OrganizationMemberListResponse>(JsonOptions);
        list.ShouldNotBeNull();
        list!.Members.ShouldContain(m => m.UserId == memberId);

        var deleteResponse = await client.DeleteAsync($"/admin/organizations/{organizationId}/members/{memberId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listAfterDelete = await client.GetAsync($"/admin/organizations/{organizationId}/members");
        listAfterDelete.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await listAfterDelete.Content.ReadFromJsonAsync<OrganizationMemberListResponse>(JsonOptions);
        after.ShouldNotBeNull();
        after!.Members.ShouldNotContain(m => m.UserId == memberId);
    }

    [Fact]
    public async Task User_Header_For_Unowned_Organization_Is_Forbidden()
    {
        var organizationId = await CreateOrganizationAsync($"org-header-{Guid.NewGuid():N}", "Header Org");
        var (userId, token) = await CreateStandardUserAndTokenAsync("header-user@example.com", "UserPass!2345");

        using (var scope = _factory.Services.CreateScope())
        {
            var membershipService = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipService>();
            var memberships = await membershipService.GetMembershipsForUserAsync(userId, null, default);
            memberships.ShouldBeEmpty();
        }

        using (var invalidClient = CreateAuthorizedClient(token))
        {
            invalidClient.DefaultRequestHeaders.Add(OrganizationContextHeaderNames.OrganizationId, "not-a-guid");
            var invalidResponse = await invalidClient.GetAsync("/users/me/organizations");
            invalidResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using var client = CreateAuthorizedClient(token);
        client.DefaultRequestHeaders.Add(OrganizationContextHeaderNames.OrganizationId, organizationId.ToString("D"));

        var response = await client.GetAsync("/users/me/organizations");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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

    private HttpClient CreateTokenClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(string email, string password, bool includeAdminScope)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();
        var roleContext = scope.ServiceProvider.GetRequiredService<IRoleDbContext>();
        var seededRoles = await roleContext.Roles.Select(r => r.Name).ToListAsync();
        seededRoles.ShouldContain("IdentityAdmin");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleAssignmentService = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            existing = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Admin Tester"
            };

            var createResult = await userManager.CreateAsync(existing, password);
            createResult.Succeeded.ShouldBeTrue(createResult.Errors.FirstOrDefault()?.Description);
        }

        await roleAssignmentService.AssignRolesAsync(existing!.Id, new[] { "IdentityAdmin" });

        var scopeValue = includeAdminScope
            ? string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api", "identity.admin" })
            : string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api" });

        using var client = CreateTokenClient();
        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.Password,
            [OpenIddictConstants.Parameters.Username] = email,
            [OpenIddictConstants.Parameters.Password] = password,
            [OpenIddictConstants.Parameters.ClientId] = "test-client",
            [OpenIddictConstants.Parameters.ClientSecret] = "test-secret",
            [OpenIddictConstants.Parameters.Scope] = scopeValue
        });
        using var response = await client.PostAsync("/connect/token", tokenRequest);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);

        using var payload = JsonDocument.Parse(responseBody);

        var accessToken = payload.RootElement.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNullOrWhiteSpace();

        return (existing.Id, accessToken!);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateStandardUserAndTokenAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            existing = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                DisplayName = "Organization Tester"
            };

            var createResult = await userManager.CreateAsync(existing, password);
            createResult.Succeeded.ShouldBeTrue(createResult.Errors.FirstOrDefault()?.Description);
        }

        using var client = CreateTokenClient();
        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.Password,
            [OpenIddictConstants.Parameters.Username] = email,
            [OpenIddictConstants.Parameters.Password] = password,
            [OpenIddictConstants.Parameters.ClientId] = "test-client",
            [OpenIddictConstants.Parameters.ClientSecret] = "test-secret",
            [OpenIddictConstants.Parameters.Scope] = string.Join(' ', new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, "identity.api" })
        });
        using var response = await client.PostAsync("/connect/token", tokenRequest);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);

        using var payload = JsonDocument.Parse(responseBody);

        var accessToken = payload.RootElement.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNullOrWhiteSpace();

        return (existing!.Id, accessToken!);
    }

    private async Task<Guid> CreateOrganizationAsync(string slug, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var organizationService = scope.ServiceProvider.GetRequiredService<IOrganizationService>();

        var organization = await organizationService.CreateAsync(new OrganizationCreateRequest
        {
            Slug = slug,
            DisplayName = displayName
        });

        return organization.Id;
    }

    private async Task AddMembershipAsync(Guid organizationId, Guid userId, bool isPrimary)
    {
        using var scope = _factory.Services.CreateScope();
        var membershipService = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipService>();

        await membershipService.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organizationId,
            UserId = userId,
            IsPrimary = isPrimary,
            RoleIds = Array.Empty<Guid>()
        });
    }

    private sealed record OrganizationDto(Guid Id, string Slug, string DisplayName, OrganizationStatus Status);

    private sealed record OrganizationMembershipDto(Guid OrganizationId, Guid UserId, bool IsPrimary, Guid[] RoleIds);

    private sealed record OrganizationMemberListResponse(int Page, int PageSize, int TotalCount, OrganizationMembershipDto[] Members);

}

public sealed class OrganizationApiFactory : IdentityApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddIdentityBaseOrganizations(options =>
            {
                options.UseInMemoryDatabase("IdentityBaseTests_Organizations")
                    .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            });
            services.AddSingleton<IStartupFilter, OrganizationStartupFilter>();
        });
    }

    private sealed class OrganizationStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseAuthentication();
                app.UseOrganizationContextFromHeader();
                next(app);

                if (app.Properties.TryGetValue("__EndpointRouteBuilder", out var value) &&
                    value is IEndpointRouteBuilder endpoints)
                {
                    endpoints.MapIdentityBaseOrganizationEndpoints();
                }
            };
        }
    }
}
