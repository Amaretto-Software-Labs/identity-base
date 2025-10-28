using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Identity;
using Identity.Base.Roles;
using Identity.Base.Roles.Claims;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Identity.Base.Tests.Roles;

public class PermissionClaimsAugmentorTests
{
    private static IdentityRolesDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new IdentityRolesDbContext(options);
    }

    [Fact]
    public async Task AddsPermissionsClaim_WhenUserHasPermissions()
    {
        using var context = CreateContext(nameof(AddsPermissionsClaim_WhenUserHasPermissions));

        var permissionRead = new Permission { Name = "users.read" };
        var permissionUpdate = new Permission { Name = "users.update" };
        context.Permissions.AddRange(permissionRead, permissionUpdate);

        var role = new Role { Name = "SupportAgent" };
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        context.RolePermissions.AddRange(
            new RolePermission { RoleId = role.Id, PermissionId = permissionRead.Id },
            new RolePermission { RoleId = role.Id, PermissionId = permissionUpdate.Id });

        var userId = Guid.NewGuid();
        context.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        await context.SaveChangesAsync();

        var roleService = new RoleAssignmentService(context, NullLogger<RoleAssignmentService>.Instance);
        var resolver = new CompositePermissionResolver(roleService, Array.Empty<IAdditionalPermissionSource>());
        var augmentor = new PermissionClaimsAugmentor(resolver, new NullTenantContextAccessor(), new DefaultPermissionClaimFormatter());

        var user = new ApplicationUser { Id = userId, Email = "support@example.com" };
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        await augmentor.AugmentAsync(user, principal);

        var permissionClaims = principal.FindAll(RoleClaimTypes.Permissions).ToList();
        permissionClaims.Count.ShouldBe(1);
        permissionClaims[0].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderBy(value => value)
            .ToArray()
            .ShouldBe(new[] { "users.read", "users.update" }.OrderBy(value => value).ToArray());
    }

    [Fact]
    public async Task MergesWithExistingPermissionClaim()
    {
        using var context = CreateContext(nameof(MergesWithExistingPermissionClaim));

        var permissionDelete = new Permission { Name = "users.delete" };
        context.Permissions.Add(permissionDelete);

        var role = new Role { Name = "Admin" };
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionDelete.Id });
        var userId = Guid.NewGuid();
        context.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        await context.SaveChangesAsync();

        var roleService = new RoleAssignmentService(context, NullLogger<RoleAssignmentService>.Instance);
        var resolver = new CompositePermissionResolver(roleService, Array.Empty<IAdditionalPermissionSource>());
        var augmentor = new PermissionClaimsAugmentor(resolver, new NullTenantContextAccessor(), new DefaultPermissionClaimFormatter());

        var user = new ApplicationUser { Id = userId, Email = "admin@example.com" };
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(RoleClaimTypes.Permissions, "users.read users.update"));
        var principal = new ClaimsPrincipal(identity);

        await augmentor.AugmentAsync(user, principal);

        var permissions = principal.FindAll(RoleClaimTypes.Permissions)
            .Single()
            .Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        permissions
            .OrderBy(value => value)
            .ToArray()
            .ShouldBe(new[] { "users.read", "users.update", "users.delete" }.OrderBy(value => value).ToArray());
    }
}
