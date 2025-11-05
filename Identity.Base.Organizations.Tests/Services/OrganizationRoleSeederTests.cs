using System;
using System.Collections.Generic;
using System.Linq;
using Identity.Base.Identity;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles;
using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Identity.Base.Organizations.Tests.Services;

public class OrganizationRoleSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesDefaultRolesWithUserScopedPermissions()
    {
        await using var organizationContext = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"org-seeder-{Guid.NewGuid()}")
            .Options);

        await using var roleContext = new IdentityRolesDbContext(new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"role-seeder-{Guid.NewGuid()}")
            .Options);

        var permissionNames = new[]
        {
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationsManage,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead,
            UserOrganizationPermissions.OrganizationRolesManage
        };

        foreach (var name in permissionNames)
        {
            roleContext.Permissions.Add(new Permission
            {
                Name = name,
                Description = name
            });
        }

        await roleContext.SaveChangesAsync();

        var services = new ServiceCollection().BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationRoleOptions());
        var seedCallbacks = new IdentityBaseSeedCallbacks();
        var seeder = new OrganizationRoleSeeder(
            organizationContext,
            options,
            seedCallbacks,
            services,
            NullLogger<OrganizationRoleSeeder>.Instance,
            roleContext);

        await seeder.SeedAsync();

        var roles = await organizationContext.OrganizationRoles.ToListAsync();
        roles.Count.ShouldBe(3);

        var ownerRole = roles.Single(role => role.Name == options.Value.OwnerRoleName);
        var managerRole = roles.Single(role => role.Name == options.Value.ManagerRoleName);
        var memberRole = roles.Single(role => role.Name == options.Value.MemberRoleName);

        await AssertRolePermissionsAsync(organizationContext, roleContext, ownerRole.Id, new[]
        {
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationsManage,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead,
            UserOrganizationPermissions.OrganizationRolesManage
        });

        await AssertRolePermissionsAsync(organizationContext, roleContext, managerRole.Id, new[]
        {
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead
        });

        await AssertRolePermissionsAsync(organizationContext, roleContext, memberRole.Id, new[]
        {
            UserOrganizationPermissions.OrganizationsRead
        });
    }

    private static async Task AssertRolePermissionsAsync(
        OrganizationDbContext organizationContext,
        IdentityRolesDbContext roleContext,
        Guid roleId,
        IReadOnlyCollection<string> expectedNames)
    {
        var permissionIds = await organizationContext.OrganizationRolePermissions
            .Where(assignment => assignment.RoleId == roleId)
            .Select(assignment => assignment.PermissionId)
            .ToListAsync();

        var permissionNames = await roleContext.Permissions
            .Where(permission => permissionIds.Contains(permission.Id))
            .Select(permission => permission.Name)
            .OrderBy(name => name)
            .ToListAsync();

        permissionNames.ShouldBe(expectedNames.OrderBy(name => name).ToArray());
    }
}
