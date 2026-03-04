using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Roles;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Options;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Identity.Base.Tests.Roles;

public class RoleSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesPermissionsAndRoles()
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"role-seeder-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        await using var context = new IdentityRolesDbContext(options);

        var permissionOptions = OptionsFactory.Create(new PermissionCatalogOptions
        {
            Definitions =
            {
                new PermissionDefinition { Name = "users.read", Description = "Read users" },
                new PermissionDefinition { Name = "users.create", Description = "Create users" },
                new PermissionDefinition { Name = "roles.manage", Description = "Manage roles" }
            }
        });

        var roleOptions = OptionsFactory.Create(new RoleConfigurationOptions
        {
            Definitions =
            {
                new RoleDefinition
                {
                    Name = "IdentityAdmin",
                    Description = "Admin",
                    IsSystemRole = true,
                    Permissions = new List<string> { "users.read", "users.create", "roles.manage" }
                },
                new RoleDefinition
                {
                    Name = "StandardUser",
                    Description = "Standard",
                    IsSystemRole = false,
                    Permissions = new List<string> { "users.read" }
                }
            }
        });

        var seeder = new RoleSeeder(
            context,
            roleOptions,
            permissionOptions,
            NullLogger<RoleSeeder>.Instance,
            new IdentityBaseSeedCallbacks(),
            new ServiceCollection().BuildServiceProvider());

        await seeder.SeedAsync();

        var permissions = await context.Permissions.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        permissions.Select(p => p.Name).ToArray().ShouldBe(new[] { "roles.manage", "users.create", "users.read" });
        permissions.First(p => p.Name == "users.read").Description.ShouldBe("Read users");

        var roles = await context.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
        roles.Count.ShouldBe(2);
        roles.First(r => r.Name == "IdentityAdmin").Description.ShouldBe("Admin");
        roles.First(r => r.Name == "StandardUser").IsSystemRole.ShouldBeFalse();

        var rolePermissions = await context.RolePermissions.AsNoTracking().ToListAsync();
        rolePermissions.Count.ShouldBe(4); // Admin:3, Standard:1

        var adminRole = roles.First(r => r.Name == "IdentityAdmin");
        var adminPermissionIds = rolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Join(context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
            .ToList();
        adminPermissionIds.ShouldBe(new[] { "users.read", "users.create", "roles.manage" }, ignoreOrder: true);

        var standardRole = roles.First(r => r.Name == "StandardUser");
        var standardPermissionIds = rolePermissions
            .Where(rp => rp.RoleId == standardRole.Id)
            .Join(context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
            .ToList();
        standardPermissionIds.ShouldBe(new[] { "users.read" });
    }

    [Fact]
    public async Task SeedAsync_UpdatesExistingRolesAndPermissions()
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"role-seeder-update-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        await using var context = new IdentityRolesDbContext(options);

        var permission = new Permission { Id = Guid.NewGuid(), Name = "users.read", Description = "Old" };
        context.Permissions.Add(permission);

        var role = new Role { Id = Guid.NewGuid(), Name = "IdentityAdmin", Description = "Old", IsSystemRole = false };
        context.Roles.Add(role);
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = Guid.NewGuid() }); // obsolete permission
        await context.SaveChangesAsync();

        var permissionOptions = OptionsFactory.Create(new PermissionCatalogOptions
        {
            Definitions =
            {
                new PermissionDefinition { Name = "users.read", Description = "Updated" },
                new PermissionDefinition { Name = "users.create", Description = "Create" }
            }
        });

        var roleOptions = OptionsFactory.Create(new RoleConfigurationOptions
        {
            Definitions =
            {
                new RoleDefinition
                {
                    Name = "IdentityAdmin",
                    Description = "Updated",
                    IsSystemRole = true,
                    Permissions = new List<string> { "users.read", "users.create" }
                }
            }
        });

        var seeder = new RoleSeeder(
            context,
            roleOptions,
            permissionOptions,
            NullLogger<RoleSeeder>.Instance,
            new IdentityBaseSeedCallbacks(),
            new ServiceCollection().BuildServiceProvider());

        await seeder.SeedAsync();

        var dbPermission = await context.Permissions.SingleAsync(p => p.Name == "users.read");
        dbPermission.Description.ShouldBe("Updated");

        var adminRole = await context.Roles.SingleAsync(r => r.Name == "IdentityAdmin");
        adminRole.Description.ShouldBe("Updated");
        adminRole.IsSystemRole.ShouldBeTrue();

        var adminPermissions = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Join(context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
            .ToListAsync();
        adminPermissions.ShouldBe(new[] { "users.read", "users.create" }, ignoreOrder: true);
    }

    [Fact]
    public async Task SeedAsync_UpdatesStandardUserPermissions_WhenRoleAndPermissionsAlreadyExist()
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"role-seeder-standarduser-update-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        await using var context = new IdentityRolesDbContext(options);

        var usersRead = new Permission { Id = Guid.NewGuid(), Name = "users.read", Description = "Read" };
        var usersCreate = new Permission { Id = Guid.NewGuid(), Name = "users.create", Description = "Create" };
        context.Permissions.AddRange(usersRead, usersCreate);

        var standardUser = new Role
        {
            Id = Guid.NewGuid(),
            Name = "StandardUser",
            Description = "Existing Standard User",
            IsSystemRole = false
        };
        context.Roles.Add(standardUser);
        context.RolePermissions.Add(new RolePermission { RoleId = standardUser.Id, PermissionId = usersRead.Id });
        await context.SaveChangesAsync();

        var permissionOptions = OptionsFactory.Create(new PermissionCatalogOptions
        {
            Definitions =
            {
                new PermissionDefinition { Name = "users.read", Description = "Read" },
                new PermissionDefinition { Name = "users.create", Description = "Create" }
            }
        });

        var roleOptions = OptionsFactory.Create(new RoleConfigurationOptions
        {
            Definitions =
            {
                new RoleDefinition
                {
                    Name = "StandardUser",
                    Description = "Updated Standard User",
                    IsSystemRole = false,
                    Permissions = new List<string> { "users.read", "users.create" }
                }
            }
        });

        var seeder = new RoleSeeder(
            context,
            roleOptions,
            permissionOptions,
            NullLogger<RoleSeeder>.Instance,
            new IdentityBaseSeedCallbacks(),
            new ServiceCollection().BuildServiceProvider());

        await seeder.SeedAsync();

        var dbRole = await context.Roles.SingleAsync(role => role.Name == "StandardUser");
        dbRole.Description.ShouldBe("Updated Standard User");

        var standardUserPermissions = await context.RolePermissions
            .Where(rp => rp.RoleId == dbRole.Id)
            .Join(context.Permissions, rp => rp.PermissionId, p => p.Id, (_, permission) => permission.Name)
            .ToListAsync();

        standardUserPermissions.ShouldBe(new[] { "users.read", "users.create" }, ignoreOrder: true);
    }

    [Fact]
    public async Task SeedAsync_DoesNotDuplicatePermissions_ForWhitespacePaddedRoleNames()
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase($"role-seeder-whitespace-role-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        var roleName = " StandardUser ";

        await using (var arrangeContext = new IdentityRolesDbContext(options))
        {
            var usersRead = new Permission { Id = Guid.NewGuid(), Name = "users.read", Description = "Read" };
            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                Description = "Whitespace role",
                IsSystemRole = false
            };

            arrangeContext.Permissions.Add(usersRead);
            arrangeContext.Roles.Add(role);
            arrangeContext.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = usersRead.Id
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using (var seedingContext = new IdentityRolesDbContext(options))
        {
            var permissionOptions = OptionsFactory.Create(new PermissionCatalogOptions
            {
                Definitions =
                {
                    new PermissionDefinition { Name = "users.read", Description = "Read" }
                }
            });

            var roleOptions = OptionsFactory.Create(new RoleConfigurationOptions
            {
                Definitions =
                {
                    new RoleDefinition
                    {
                        Name = roleName,
                        Description = "Whitespace role",
                        IsSystemRole = false,
                        Permissions = new List<string> { "users.read" }
                    }
                }
            });

            var seeder = new RoleSeeder(
                seedingContext,
                roleOptions,
                permissionOptions,
                NullLogger<RoleSeeder>.Instance,
                new IdentityBaseSeedCallbacks(),
                new ServiceCollection().BuildServiceProvider());

            await seeder.SeedAsync();
        }

        await using var assertContext = new IdentityRolesDbContext(options);
        var roleId = await assertContext.Roles
            .Where(role => role.Name == roleName)
            .Select(role => role.Id)
            .SingleAsync();

        var rolePermissionCount = await assertContext.RolePermissions
            .CountAsync(rolePermission => rolePermission.RoleId == roleId);

        rolePermissionCount.ShouldBe(1);
    }
}
