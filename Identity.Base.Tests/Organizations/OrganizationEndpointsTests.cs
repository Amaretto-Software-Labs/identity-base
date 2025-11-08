using System;
using System.Collections.Generic;
using System.Linq;
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
using Identity.Base.Organizations.Authorization;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Entities;
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

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<OrganizationDto>>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.Items.ShouldContain(item => item.Id == organizationId);

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
    public async Task Admin_Endpoints_Require_Admin_Scope()
    {
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-missing-scope@example.com", "AdminPass!2345", includeAdminScope: false);

        using var client = CreateAuthorizedClient(token);

        var response = await client.GetAsync("/admin/organizations");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_ReadOnly_Role_Cannot_Manage_Organizations()
    {
        var roleName = await EnsureRoleWithPermissionsAsync($"OrgReadOnlyAdmin-{Guid.NewGuid():N}", AdminOrganizationPermissions.OrganizationsRead);
        var (_, token) = await CreateAdminUserAndTokenAsync("admin-readonly@example.com", "AdminPass!2345", includeAdminScope: true, roleNames: new[] { roleName });

        using var client = CreateAuthorizedClient(token);

        var listResponse = await client.GetAsync("/admin/organizations");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var createResponse = await client.PostAsJsonAsync("/admin/organizations", new
        {
            Slug = $"readonly-{Guid.NewGuid():N}",
            DisplayName = "Read Only Org",
            Metadata = new Dictionary<string, string?>()
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var organizationId = await CreateOrganizationAsync($"org-readonly-target-{Guid.NewGuid():N}", "Target Org");

        var patchResponse = await client.PatchAsJsonAsync($"/admin/organizations/{organizationId}", new
        {
            DisplayName = "Should Not Update"
        });
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_Endpoints_Return_Memberships_And_Support_Header()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-{Guid.NewGuid():N}", "User Org");
        var (userId, _) = await CreateStandardUserAndTokenAsync("org-member@example.com", "UserPass!2345");

        await AddMembershipAsync(organizationId, userId, isPrimary: true, assignOwnerRole: true);

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
    public async Task User_Can_Get_Organization_Detail()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-detail-{Guid.NewGuid():N}", "Detail Org");
        var (userId, token) = await CreateStandardUserAndTokenAsync("org-detail@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, userId, isPrimary: true, assignOwnerRole: true);

        using var client = CreateAuthorizedClient(token);
        var response = await client.GetAsync($"/users/me/organizations/{organizationId:D}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<OrganizationDto>(JsonOptions);
        dto.ShouldNotBeNull();
        dto!.Id.ShouldBe(organizationId);
    }

    [Fact]
    public async Task User_Can_Patch_Organization_Through_User_Endpoints()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-patch-{Guid.NewGuid():N}", "Patch Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-patch@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        using var client = CreateAuthorizedClient(ownerToken);
        var response = await client.PatchAsJsonAsync($"/users/me/organizations/{organizationId:D}", new
        {
            DisplayName = "User Patched Org"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrganizationDto>(JsonOptions);
        dto.ShouldNotBeNull();
        dto!.DisplayName.ShouldBe("User Patched Org");
    }

    [Fact]
    public async Task User_Can_List_Members_Through_User_Endpoints()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-members-{Guid.NewGuid():N}", "User Members Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-members@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        var (memberId, _) = await CreateStandardUserAndTokenAsync("member-to-add@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, memberId, isPrimary: false);

        using var client = CreateAuthorizedClient(ownerToken);

        var listResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/members");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await listResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationMembershipDto>>(JsonOptions);
        payload.ShouldNotBeNull();
        payload!.Items.ShouldContain(m => m.UserId == ownerId);
        payload.Items.ShouldContain(m => m.UserId == memberId);

        var createResponse = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/members", new
        {
            UserId = memberId,
            IsPrimary = false,
            RoleIds = Array.Empty<Guid>()
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task User_Member_List_Supports_Paging_And_Search()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-members-paging-{Guid.NewGuid():N}", "User Members Paging Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-members-paging@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        var (searchTargetId, _) = await CreateStandardUserAndTokenAsync("search-target@example.com", "UserPass!2345");
        var (secondaryId, _) = await CreateStandardUserAndTokenAsync("secondary-member@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, searchTargetId, isPrimary: false);
        await AddMembershipAsync(organizationId, secondaryId, isPrimary: false);

        using var client = CreateAuthorizedClient(ownerToken);

        var pagedResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/members?page=2&pageSize=1");
        pagedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paged = await pagedResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationMembershipDto>>(JsonOptions);
        paged.ShouldNotBeNull();
        paged!.Page.ShouldBe(2);
        paged.PageSize.ShouldBe(1);
        paged.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        paged.Items.Count.ShouldBe(1);

        var searchResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/members?search=search-target");
        searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var search = await searchResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationMembershipDto>>(JsonOptions);
        search.ShouldNotBeNull();
        search!.Items.Count.ShouldBe(1);
        search.Items[0].UserId.ShouldBe(searchTargetId);
    }

    [Fact]
    public async Task User_Can_Manage_Memberships_Via_User_Endpoints()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-members-crud-{Guid.NewGuid():N}", "User Members CRUD");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-members-crud@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        var (memberId, _) = await CreateStandardUserAndTokenAsync("member-crud@example.com", "UserPass!2345");

        using var client = CreateAuthorizedClient(ownerToken);

        var addResponse = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/members", new
        {
            UserId = memberId,
            IsPrimary = false,
            RoleIds = Array.Empty<Guid>()
        });
        addResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await addResponse.Content.ReadFromJsonAsync<OrganizationMembershipDto>(JsonOptions);
        created.ShouldNotBeNull();

        var updateResponse = await client.PutAsJsonAsync($"/users/me/organizations/{organizationId:D}/members/{memberId:D}", new
        {
            IsPrimary = true
        });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync($"/users/me/organizations/{organizationId:D}/members/{memberId:D}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task User_Can_Manage_Roles_Via_User_Endpoints()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-roles-{Guid.NewGuid():N}", "User Roles Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-roles@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        using var client = CreateAuthorizedClient(ownerToken);

        var createResponse = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/roles", new
        {
            Name = $"TeamLead-{Guid.NewGuid():N}",
            Description = "Team lead role",
            IsSystemRole = false
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdRole = await createResponse.Content.ReadFromJsonAsync<OrganizationRoleDto>(JsonOptions);
        createdRole.ShouldNotBeNull();

        var listResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/roles");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roles = await listResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationRoleDto>>(JsonOptions);
        roles.ShouldNotBeNull();
        roles!.Items.ShouldContain(role => role.Id == createdRole!.Id);

        var permissionsResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/roles/{createdRole!.Id}/permissions");
        permissionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updatePermissions = await client.PutAsJsonAsync($"/users/me/organizations/{organizationId:D}/roles/{createdRole.Id}/permissions", new
        {
            Permissions = new[] { "user.organizations.members.read" }
        });
        updatePermissions.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deleteResponse = await client.DeleteAsync($"/users/me/organizations/{organizationId:D}/roles/{createdRole.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task User_Role_List_Supports_Paging_And_Search()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-roles-paging-{Guid.NewGuid():N}", "User Roles Paging Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-roles-paging@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        using var client = CreateAuthorizedClient(ownerToken);

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/roles", new
            {
                Name = $"PagingRole-{i}-{Guid.NewGuid():N}",
                Description = "Paging validation role",
                IsSystemRole = false
            });
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var pagedResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/roles?page=2&pageSize=1&sort=name:asc");
        pagedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paged = await pagedResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationRoleDto>>(JsonOptions);
        paged.ShouldNotBeNull();
        paged!.Page.ShouldBe(2);
        paged.PageSize.ShouldBe(1);
        paged.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        paged.Items.Count.ShouldBe(1);

        var searchResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/roles?search=PagingRole-1");
        searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var search = await searchResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationRoleDto>>(JsonOptions);
        search.ShouldNotBeNull();
        search!.Items.ShouldContain(role => role.Name.Contains("PagingRole-1", StringComparison.OrdinalIgnoreCase));
        search.Items.ShouldAllBe(role => role.Name.Contains("PagingRole-1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task User_Can_Manage_Invitations_Via_User_Endpoints()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-invitations-{Guid.NewGuid():N}", "User Invitation Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-invite@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        using var client = CreateAuthorizedClient(ownerToken);
        var invitationEmail = $"invite-{Guid.NewGuid():N}@example.com";

        var createResponse = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/invitations", new
        {
            Email = invitationEmail
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdInvitation = await createResponse.Content.ReadFromJsonAsync<OrganizationInvitationDto>(JsonOptions);
        createdInvitation.ShouldNotBeNull();

        var listResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/invitations");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var invitations = await listResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationInvitationDto>>(JsonOptions);
        invitations.ShouldNotBeNull();
        invitations!.Items.ShouldContain(invite => invite.Code == createdInvitation!.Code);

        var deleteResponse = await client.DeleteAsync($"/users/me/organizations/{organizationId:D}/invitations/{createdInvitation.Code:D}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task User_Invitation_List_Supports_Paging_And_Search()
    {
        var organizationId = await CreateOrganizationAsync($"org-user-invitations-paging-{Guid.NewGuid():N}", "User Invitations Paging Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-invite-paging@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, ownerId, isPrimary: true, assignOwnerRole: true);

        using var client = CreateAuthorizedClient(ownerToken);

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/invitations", new
            {
                Email = $"paging-invite-{i}-{Guid.NewGuid():N}@example.com"
            });
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var pagedResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/invitations?page=2&pageSize=1&sort=email:asc");
        pagedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paged = await pagedResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationInvitationDto>>(JsonOptions);
        paged.ShouldNotBeNull();
        paged!.Page.ShouldBe(2);
        paged.PageSize.ShouldBe(1);
        paged.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        paged.Items.Count.ShouldBe(1);

        var searchResponse = await client.GetAsync($"/users/me/organizations/{organizationId:D}/invitations?search=paging-invite-1");
        searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var search = await searchResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationInvitationDto>>(JsonOptions);
        search.ShouldNotBeNull();
        search!.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
        search.Items.ShouldAllBe(invite => invite.Email!.Contains("paging-invite-1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task User_Cannot_Access_Organization_Outside_Their_Scope()
    {
        var inScopeOrgId = await CreateOrganizationAsync($"org-user-scope-{Guid.NewGuid():N}", "User Scope Org");
        var outOfScopeOrgId = await CreateOrganizationAsync($"org-user-outside-{Guid.NewGuid():N}", "User Outside Org");
        var (ownerId, ownerToken) = await CreateStandardUserAndTokenAsync("owner-scope@example.com", "UserPass!2345");
        await AddMembershipAsync(inScopeOrgId, ownerId, isPrimary: true, assignOwnerRole: true);

        using var client = CreateAuthorizedClient(ownerToken);

        var detailResponse = await client.GetAsync($"/users/me/organizations/{outOfScopeOrgId:D}");
        detailResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var createResponse = await client.PostAsJsonAsync($"/users/me/organizations/{outOfScopeOrgId:D}/members", new
        {
            UserId = ownerId,
            IsPrimary = false,
            RoleIds = Array.Empty<Guid>()
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrgMember_Cannot_Manage_Another_Organization()
    {
        var ownerOrgId = await CreateOrganizationAsync($"org-owner-scope-{Guid.NewGuid():N}", "Owner Scope Org");
        var restrictedOrgId = await CreateOrganizationAsync($"org-restricted-{Guid.NewGuid():N}", "Restricted Org");
        var (actorId, actorToken) = await CreateStandardUserAndTokenAsync("dual-role@example.com", "UserPass!2345");
        await AddMembershipAsync(ownerOrgId, actorId, isPrimary: true, assignOwnerRole: true);
        await AddMembershipAsync(restrictedOrgId, actorId, isPrimary: false, systemRoleName: "OrgMember");

        var (targetUserId, _) = await CreateStandardUserAndTokenAsync("restricted-target@example.com", "UserPass!2345");

        using var client = CreateAuthorizedClient(actorToken);

        var memberResponse = await client.PostAsJsonAsync($"/users/me/organizations/{restrictedOrgId:D}/members", new
        {
            UserId = targetUserId,
            IsPrimary = false,
            RoleIds = Array.Empty<Guid>()
        });
        memberResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var inviteResponse = await client.PostAsJsonAsync($"/users/me/organizations/{restrictedOrgId:D}/invitations", new
        {
            Email = $"restricted-invite-{Guid.NewGuid():N}@example.com"
        });
        inviteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var roleResponse = await client.PostAsJsonAsync($"/users/me/organizations/{restrictedOrgId:D}/roles", new
        {
            Name = $"RestrictedRole-{Guid.NewGuid():N}",
            Description = "Should not create",
            IsSystemRole = false
        });
        roleResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrgManager_Can_Manage_Members_But_Not_Roles()
    {
        var organizationId = await CreateOrganizationAsync($"org-manager-{Guid.NewGuid():N}", "Manager Org");
        var (managerId, managerToken) = await CreateStandardUserAndTokenAsync("org-manager@example.com", "UserPass!2345");
        await AddMembershipAsync(organizationId, managerId, isPrimary: true, systemRoleName: "OrgManager");

        var (memberId, _) = await CreateStandardUserAndTokenAsync("manager-member@example.com", "UserPass!2345");

        using var client = CreateAuthorizedClient(managerToken);

        var addMemberResponse = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/members", new
        {
            UserId = memberId,
            IsPrimary = false,
            RoleIds = Array.Empty<Guid>()
        });
        addMemberResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var inviteResponse = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/invitations", new
        {
            Email = $"manager-invite-{Guid.NewGuid():N}@example.com"
        });
        inviteResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var roleResponse = await client.PostAsJsonAsync($"/users/me/organizations/{organizationId:D}/roles", new
        {
            Name = $"ManagerRole-{Guid.NewGuid():N}",
            Description = "Manager should not create roles",
            IsSystemRole = false
        });
        roleResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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

        var list = await listResponse.Content.ReadFromJsonAsync<PagedResult<OrganizationMembershipDto>>(JsonOptions);
        list.ShouldNotBeNull();
        list!.Items.ShouldContain(m => m.UserId == memberId);

        var deleteResponse = await client.DeleteAsync($"/admin/organizations/{organizationId}/members/{memberId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listAfterDelete = await client.GetAsync($"/admin/organizations/{organizationId}/members");
        listAfterDelete.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await listAfterDelete.Content.ReadFromJsonAsync<PagedResult<OrganizationMembershipDto>>(JsonOptions);
        after.ShouldNotBeNull();
        after!.Items.ShouldNotContain(m => m.UserId == memberId);
    }

    [Fact]
    public async Task Admin_Member_Manager_Can_Manage_Members_But_Not_Roles()
    {
        var organizationId = await CreateOrganizationAsync($"org-admin-member-manager-{Guid.NewGuid():N}", "Admin Member Manager Org");

        var roleName = await EnsureRoleWithPermissionsAsync(
            $"OrgMemberManager-{Guid.NewGuid():N}",
            AdminOrganizationPermissions.OrganizationsRead,
            AdminOrganizationPermissions.OrganizationMembersRead,
            AdminOrganizationPermissions.OrganizationMembersManage,
            AdminOrganizationPermissions.OrganizationRolesRead);

        var (adminId, token) = await CreateAdminUserAndTokenAsync("admin-member-manager@example.com", "AdminPass!2345", includeAdminScope: true, roleNames: new[] { roleName });
        await AddMembershipAsync(organizationId, adminId, isPrimary: true, assignOwnerRole: true);

        var (memberId, _) = await CreateStandardUserAndTokenAsync("admin-managed-member@example.com", "UserPass!2345");

        using var client = CreateAuthorizedClient(token);

        var addMemberResponse = await client.PostAsJsonAsync($"/admin/organizations/{organizationId}/members", new
        {
            UserId = memberId,
            IsPrimary = false,
            RoleIds = Array.Empty<Guid>()
        });
        addMemberResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listRolesResponse = await client.GetAsync($"/admin/organizations/{organizationId}/roles");
        listRolesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var createRoleResponse = await client.PostAsJsonAsync($"/admin/organizations/{organizationId}/roles", new
        {
            Name = $"AdminLimitedRole-{Guid.NewGuid():N}",
            Description = "Should be forbidden",
            IsSystemRole = false
        });
        createRoleResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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

    private async Task<string> EnsureRoleWithPermissionsAsync(string roleName, params string[] permissions)
    {
        if (permissions.Length == 0)
        {
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        }

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.SeedIdentityRolesAsync();

        var roleContext = scope.ServiceProvider.GetRequiredService<IRoleDbContext>();

        var existing = await roleContext.Roles
            .Include(role => role.RolePermissions)
            .FirstOrDefaultAsync(role => role.Name == roleName);

        if (existing is not null)
        {
            return roleName;
        }

        var permissionEntities = await roleContext.Permissions
            .Where(permission => permissions.Contains(permission.Name, StringComparer.OrdinalIgnoreCase))
            .ToListAsync();

        if (permissionEntities.Count != permissions.Length)
        {
            throw new InvalidOperationException($"One or more permissions were not found for role '{roleName}'.");
        }

        var role = new Role
        {
            Name = roleName,
            Description = "Test role",
            IsSystemRole = false
        };

        foreach (var permission in permissionEntities)
        {
            role.RolePermissions.Add(new RolePermission
            {
                PermissionId = permission.Id,
                Permission = permission
            });
        }

        roleContext.Roles.Add(role);
        await roleContext.SaveChangesAsync();

        return roleName;
    }

    private async Task<(Guid UserId, string AccessToken)> CreateAdminUserAndTokenAsync(string email, string password, bool includeAdminScope, IEnumerable<string>? roleNames = null)
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

        var rolesToAssign = roleNames?.ToArray() ?? new[] { "IdentityAdmin" };
        await roleAssignmentService.AssignRolesAsync(existing!.Id, rolesToAssign);

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

    private async Task AddMembershipAsync(Guid organizationId, Guid userId, bool isPrimary, bool assignOwnerRole = false, string? systemRoleName = null)
    {
        if (assignOwnerRole && !string.IsNullOrWhiteSpace(systemRoleName))
        {
            throw new ArgumentException("Specify either assignOwnerRole or systemRoleName, not both.", nameof(systemRoleName));
        }

        using var scope = _factory.Services.CreateScope();
        var membershipService = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipService>();
        var roleIds = Array.Empty<Guid>();

        if (!string.IsNullOrWhiteSpace(systemRoleName))
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var roleId = await dbContext.OrganizationRoles
                .AsNoTracking()
                .Where(role => role.OrganizationId == null && role.Name == systemRoleName)
                .Select(role => role.Id)
                .FirstAsync();

            roleIds = new[] { roleId };
        }
        else if (assignOwnerRole)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var ownerRoleId = await dbContext.OrganizationRoles
                .AsNoTracking()
                .Where(role => role.OrganizationId == null && role.Name == "OrgOwner")
                .Select(role => role.Id)
                .FirstAsync();

            roleIds = new[] { ownerRoleId };
        }

        await membershipService.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organizationId,
            UserId = userId,
            IsPrimary = isPrimary,
            RoleIds = roleIds
        });
    }

    private sealed record OrganizationDto(Guid Id, string Slug, string DisplayName, OrganizationStatus Status);

    private sealed record OrganizationMembershipDto(Guid OrganizationId, Guid UserId, bool IsPrimary, Guid[] RoleIds);

    private sealed record OrganizationMemberListDto(int Page, int PageSize, int TotalCount, OrganizationMembershipDto[] Members);

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
