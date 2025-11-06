using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Options;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Identity.Base.Tests.Organizations;

public class OrganizationRoleSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesDefaultOrganizationRoles()
    {
        var roleDbOptions = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"org-role-seeder-rbac-{Guid.NewGuid():N}")
            .Options;
        await using var roleContext = new IdentityRolesDbContext(roleDbOptions);

        // Seed permissions expected by organization roles
        roleContext.Permissions.AddRange(new Permission
        {
            Name = "user.organizations.read"
        }, new Permission
        {
            Name = "user.organizations.manage"
        }, new Permission
        {
            Name = "user.organizations.members.read"
        }, new Permission
        {
            Name = "user.organizations.members.manage"
        }, new Permission
        {
            Name = "user.organizations.roles.read"
        }, new Permission
        {
            Name = "user.organizations.roles.manage"
        });
        await roleContext.SaveChangesAsync();

        var orgDbOptions = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"org-role-seeder-{Guid.NewGuid():N}")
            .Options;
        await using var orgContext = new OrganizationDbContext(orgDbOptions);

        var organizationRoleOptions = Options.Create(new OrganizationRoleOptions
        {
            DefaultRoles = new List<OrganizationRoleDefinitionOptions>
            {
                new OrganizationRoleDefinitionOptions
                {
                    DefaultType = OrganizationRoleDefaultType.Owner,
                    Name = "OrgOwner",
                    Description = "Owner",
                    Permissions = new List<string>
                    {
                        "user.organizations.read",
                        "user.organizations.manage",
                        "user.organizations.members.read",
                        "user.organizations.members.manage",
                        "user.organizations.roles.read",
                        "user.organizations.roles.manage"
                    }
                },
                new OrganizationRoleDefinitionOptions
                {
                    DefaultType = OrganizationRoleDefaultType.Manager,
                    Name = "OrgManager",
                    Description = "Manager",
                    Permissions = new List<string>
                    {
                        "user.organizations.read",
                        "user.organizations.members.read",
                        "user.organizations.members.manage"
                    }
                },
                new OrganizationRoleDefinitionOptions
                {
                    DefaultType = OrganizationRoleDefaultType.Member,
                    Name = "OrgMember",
                    Description = "Member",
                    Permissions = new List<string>
                    {
                        "user.organizations.read"
                    }
                }
            }
        });

        var seeder = new OrganizationRoleSeeder(
            orgContext,
            organizationRoleOptions,
            new IdentityBaseSeedCallbacks(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<OrganizationRoleSeeder>.Instance,
            roleContext);

        await seeder.SeedAsync();

        var roles = await orgContext.OrganizationRoles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
        roles.Count.ShouldBe(3);
        roles.ShouldContain(r => r.Name == "OrgOwner" && r.Description == "Owner");
        roles.ShouldContain(r => r.Name == "OrgManager" && r.Description == "Manager");
        roles.ShouldContain(r => r.Name == "OrgMember" && r.Description == "Member");

        var ownerRole = roles.First(r => r.Name == "OrgOwner");
        var ownerPermissions = await orgContext.OrganizationRolePermissions
            .Where(p => p.RoleId == ownerRole.Id)
            .Join(roleContext.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
            .ToListAsync();
        ownerPermissions.ShouldBe(new[]
        {
            "user.organizations.read",
            "user.organizations.manage",
            "user.organizations.members.read",
            "user.organizations.members.manage",
            "user.organizations.roles.read",
            "user.organizations.roles.manage"
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task SeedAsync_UpdatesExistingRoles()
    {
        var roleDbOptions = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"org-role-seeder-update-rbac-{Guid.NewGuid():N}")
            .Options;
        await using var roleContext = new IdentityRolesDbContext(roleDbOptions);

        roleContext.Permissions.AddRange(new Permission
        {
            Name = "user.organizations.read"
        }, new Permission
        {
            Name = "user.organizations.manage"
        });
        await roleContext.SaveChangesAsync();

        var orgDbOptions = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"org-role-seeder-update-{Guid.NewGuid():N}")
            .Options;
        await using var orgContext = new OrganizationDbContext(orgDbOptions);

        var existingRole = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            Name = "OrgOwner",
            Description = "Old desc",
            IsSystemRole = false,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        await orgContext.OrganizationRoles.AddAsync(existingRole);
        await orgContext.OrganizationRolePermissions.AddAsync(new OrganizationRolePermission
        {
            RoleId = existingRole.Id,
            PermissionId = Guid.NewGuid() // unknown permission that should be removed
        });
        await orgContext.SaveChangesAsync();

        var organizationRoleOptions = Options.Create(new OrganizationRoleOptions
        {
            DefaultRoles = new List<OrganizationRoleDefinitionOptions>
            {
                new OrganizationRoleDefinitionOptions
                {
                    DefaultType = OrganizationRoleDefaultType.Owner,
                    Name = "OrgOwner",
                    Description = "Updated owner",
                    Permissions = new List<string>
                    {
                        "user.organizations.read",
                        "user.organizations.manage"
                    }
                }
            }
        });

        var seeder = new OrganizationRoleSeeder(
            orgContext,
            organizationRoleOptions,
            new IdentityBaseSeedCallbacks(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<OrganizationRoleSeeder>.Instance,
            roleContext);

        await seeder.SeedAsync();

        var updatedRole = await orgContext.OrganizationRoles.SingleAsync(r => r.Name == "OrgOwner");
        updatedRole.Description.ShouldBe("Updated owner");
        updatedRole.IsSystemRole.ShouldBeTrue();

        var assignedPermissions = await orgContext.OrganizationRolePermissions
            .Where(p => p.RoleId == updatedRole.Id)
            .Join(roleContext.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
            .ToListAsync();
        assignedPermissions.ShouldBe(new[]
        {
            "user.organizations.read",
            "user.organizations.manage"
        }, ignoreOrder: true);
    }
}
