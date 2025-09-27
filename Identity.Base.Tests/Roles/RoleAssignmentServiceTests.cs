using System;
using System.Linq;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace Identity.Base.Tests.Roles;

public class RoleAssignmentServiceTests
{
    private static IdentityRolesDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var context = new IdentityRolesDbContext(options);
        return context;
    }

    [Fact]
    public async Task AssignRolesAsync_AddsAndRemovesRolesCorrectly()
    {
        // Arrange
        using var context = CreateContext(nameof(AssignRolesAsync_AddsAndRemovesRolesCorrectly));
        var roleA = new Role { Name = "RoleA" };
        var roleB = new Role { Name = "RoleB" };
        context.Roles.AddRange(roleA, roleB);
        context.UserRoles.Add(new UserRole { UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), RoleId = roleA.Id });
        await context.SaveChangesAsync();

        IRoleDbContext dbContext = context;
        var service = new RoleAssignmentService(dbContext, NullLogger<RoleAssignmentService>.Instance);
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        await service.AssignRolesAsync(userId, new[] { "RoleB" });

        // Assert
        var userRoles = await context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        userRoles.Should().HaveCount(1);
        userRoles[0].RoleId.Should().Be(roleB.Id);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_ReturnsUnionOfRolePermissions()
    {
        // Arrange
        using var context = CreateContext(nameof(GetEffectivePermissionsAsync_ReturnsUnionOfRolePermissions));
        var permissionRead = new Permission { Name = "users.read" };
        var permissionUpdate = new Permission { Name = "users.update" };
        context.Permissions.AddRange(permissionRead, permissionUpdate);

        var roleReader = new Role { Name = "Reader" };
        var roleEditor = new Role { Name = "Editor" };
        context.Roles.AddRange(roleReader, roleEditor);
        await context.SaveChangesAsync();

        context.RolePermissions.AddRange(
            new RolePermission { RoleId = roleReader.Id, PermissionId = permissionRead.Id },
            new RolePermission { RoleId = roleEditor.Id, PermissionId = permissionUpdate.Id });

        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        context.UserRoles.AddRange(
            new UserRole { UserId = userId, RoleId = roleReader.Id },
            new UserRole { UserId = userId, RoleId = roleEditor.Id });

        await context.SaveChangesAsync();

        IRoleDbContext dbContext = context;
        var service = new RoleAssignmentService(dbContext, NullLogger<RoleAssignmentService>.Instance);

        // Act
        var permissions = await service.GetEffectivePermissionsAsync(userId);

        // Assert
        permissions.Should().BeEquivalentTo(new[] { "users.read", "users.update" });
    }
}
