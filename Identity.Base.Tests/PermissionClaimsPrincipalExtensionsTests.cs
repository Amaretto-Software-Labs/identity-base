using System.Security.Claims;
using Identity.Base.Roles.Claims;
using Shouldly;

namespace Identity.Base.Tests;

public class PermissionClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetPermissions_DeduplicatesAndNormalizesClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(RoleClaimTypes.Permissions, "users.read users.write"),
            new Claim(RoleClaimTypes.Permissions, " users.read  USERS.DELETE ")
        ]));

        var permissions = principal.GetPermissions();
        var permissionSet = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        permissionSet.SetEquals(
        [
            "users.read",
            "users.write",
            "users.delete"
        ]).ShouldBeTrue();
    }

    [Fact]
    public void HasPermission_UsesCaseInsensitiveMatch()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(RoleClaimTypes.Permissions, "users.read users.write"),
        ]));

        principal.HasPermission("USERS.READ").ShouldBeTrue();
        principal.HasPermission("users.delete").ShouldBeFalse();
    }

    [Fact]
    public void HasAnyPermission_ReturnsTrueWhenAnyMatch()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(RoleClaimTypes.Permissions, "users.read users.write"),
        ]));

        principal.HasAnyPermission(["users.manage", "users.read"]).ShouldBeTrue();
        principal.HasAnyPermission(["users.manage", "users.delete"]).ShouldBeFalse();
    }

    [Fact]
    public void HasAllPermissions_ReturnsTrueWhenAllMatch()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(RoleClaimTypes.Permissions, "users.read users.write"),
        ]));

        principal.HasAllPermissions(["users.read", "users.write"]).ShouldBeTrue();
        principal.HasAllPermissions(["users.read", "users.delete"]).ShouldBeFalse();
    }

    [Fact]
    public void HasPermission_ReturnsFalseForEmptyInput()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(RoleClaimTypes.Permissions, "users.read"),
        ]));

        principal.HasPermission(string.Empty).ShouldBeFalse();
        principal.HasPermission("   ").ShouldBeFalse();
    }
}
