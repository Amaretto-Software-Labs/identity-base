using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Identity.Base.Identity;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Options;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FluentAssertions;

namespace Identity.Base.Tests.Roles;

public class DefaultUserRoleAssignmentListenerTests
{
    private static IdentityRolesDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new IdentityRolesDbContext(options);
    }

    private static IOptions<RoleConfigurationOptions> CreateRoleOptions(params string[] defaultRoles)
    {
        return Microsoft.Extensions.Options.Options.Create(new RoleConfigurationOptions
        {
            DefaultUserRoles = defaultRoles.ToList(),
            Definitions = defaultRoles.Select(role => new RoleDefinition
            {
                Name = role,
                Permissions = new List<string>()
            }).ToList()
        });
    }

    [Fact]
    public async Task AssignsDefaultRoles_WhenConfigured()
    {
        using var context = CreateContext(nameof(AssignsDefaultRoles_WhenConfigured));

        // Seed role definitions
        context.Roles.Add(new Role { Name = "StandardUser" });
        await context.SaveChangesAsync();

        IRoleDbContext roleDb = context;
        var roleService = new RoleAssignmentService(roleDb, NullLogger<RoleAssignmentService>.Instance);
        var listener = new DefaultUserRoleAssignmentListener(roleService, CreateRoleOptions("StandardUser"));

        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com" };

        await listener.OnUserCreatedAsync(user);

        var userRoles = await context.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
        userRoles.Should().HaveCount(1);
        var role = await context.Roles.FindAsync(userRoles[0].RoleId);
        role!.Name.Should().Be("StandardUser");
    }

    [Fact]
    public async Task DoesNothing_WhenNoDefaultRolesConfigured()
    {
        using var context = CreateContext(nameof(DoesNothing_WhenNoDefaultRolesConfigured));

        IRoleDbContext roleDb = context;
        var roleService = new RoleAssignmentService(roleDb, NullLogger<RoleAssignmentService>.Instance);
        var listener = new DefaultUserRoleAssignmentListener(roleService, CreateRoleOptions());

        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "test@example.com" };

        await listener.OnUserCreatedAsync(user);

        var userRoles = await context.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
        userRoles.Should().BeEmpty();
    }
}
