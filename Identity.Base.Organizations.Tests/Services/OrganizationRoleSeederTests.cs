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

    [Fact]
    public async Task SeedAsync_AssignsCustomPermissionsWhenDefinedInCatalog()
    {
        await using var organizationContext = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"org-seeder-custom-{Guid.NewGuid()}")
            .Options);

        await using var roleContext = new IdentityRolesDbContext(new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"role-seeder-custom-{Guid.NewGuid()}")
            .Options);

        // Seed the base user-scoped permissions + a custom one
        var customPermission = "user.organizations.custom.export";

        var allPermissionNames = new[]
        {
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationsManage,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead,
            UserOrganizationPermissions.OrganizationRolesManage,
            customPermission
        };

        foreach (var name in allPermissionNames)
        {
            roleContext.Permissions.Add(new Permission { Name = name, Description = name });
        }

        await roleContext.SaveChangesAsync();

        var services = new ServiceCollection().BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationRoleOptions());

        // Add the custom permission to the Owner default definition
        var ownerDef = options.Value.DefaultRoles.Single(r => r.DefaultType == OrganizationRoleDefaultType.Owner);
        ownerDef.Permissions.Add(customPermission);

        var seedCallbacks = new IdentityBaseSeedCallbacks();
        var seeder = new OrganizationRoleSeeder(
            organizationContext,
            options,
            seedCallbacks,
            services,
            NullLogger<OrganizationRoleSeeder>.Instance,
            roleContext);

        await seeder.SeedAsync();

        var ownerRole = await organizationContext.OrganizationRoles.SingleAsync(r => r.Name == options.Value.OwnerRoleName);
        await AssertRolePermissionsAsync(organizationContext, roleContext, ownerRole.Id, new[]
        {
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationsManage,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead,
            UserOrganizationPermissions.OrganizationRolesManage,
            customPermission
        });
    }

    [Fact]
    public async Task SeedAsync_IgnoresMissingCustomPermissions()
    {
        await using var organizationContext = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"org-seeder-missing-{Guid.NewGuid()}")
            .Options);

        await using var roleContext = new IdentityRolesDbContext(new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"role-seeder-missing-{Guid.NewGuid()}")
            .Options);

        // Seed only the default permissions; intentionally omit the custom one
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
            roleContext.Permissions.Add(new Permission { Name = name, Description = name });
        }

        await roleContext.SaveChangesAsync();

        var services = new ServiceCollection().BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationRoleOptions());

        // Add a custom permission that does not exist in the catalog
        var missingPermission = "user.organizations.custom.missing";
        var ownerDef = options.Value.DefaultRoles.Single(r => r.DefaultType == OrganizationRoleDefaultType.Owner);
        ownerDef.Permissions.Add(missingPermission);

        var seedCallbacks = new IdentityBaseSeedCallbacks();
        var seeder = new OrganizationRoleSeeder(
            organizationContext,
            options,
            seedCallbacks,
            services,
            NullLogger<OrganizationRoleSeeder>.Instance,
            roleContext);

        await seeder.SeedAsync();

        var ownerRole = await organizationContext.OrganizationRoles.SingleAsync(r => r.Name == options.Value.OwnerRoleName);

        // The missing permission should not be assigned; only defaults present
        await AssertRolePermissionsAsync(organizationContext, roleContext, ownerRole.Id, new[]
        {
            UserOrganizationPermissions.OrganizationsRead,
            UserOrganizationPermissions.OrganizationsManage,
            UserOrganizationPermissions.OrganizationMembersRead,
            UserOrganizationPermissions.OrganizationMembersManage,
            UserOrganizationPermissions.OrganizationRolesRead,
            UserOrganizationPermissions.OrganizationRolesManage
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
