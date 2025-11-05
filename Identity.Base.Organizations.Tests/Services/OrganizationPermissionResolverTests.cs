using System.Linq;
using Shouldly;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organizations.Tests.Services;

public class OrganizationPermissionResolverTests
{
    [Fact]
    public async Task GetPermissionsAsync_ReturnsGlobalAndOrganizationPermissions()
    {
        await using var organizationContext = CreateOrganizationDbContext();
        await using var roleContext = CreateRoleDbContext();

        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permissionName = UserOrganizationPermissions.OrganizationMembersManage;

        await SeedOrganizationGraphAsync(organizationContext, roleContext, organizationId, userId, permissionName);

        var resolver = new OrganizationPermissionResolver(
            organizationContext,
            new StubRoleAssignmentService(["users.read"]),
            logger: null,
            roleContext);

        var permissions = await resolver.GetPermissionsAsync(organizationId, userId);

        permissions.OrderBy(x => x).ToArray()
            .ShouldBe(new[] { "users.read", permissionName }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task GetOrganizationPermissionsAsync_ReturnsEmpty_WhenMembershipMissing()
    {
        await using var organizationContext = CreateOrganizationDbContext();
        await using var roleContext = CreateRoleDbContext();

        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedOrganizationGraphAsync(organizationContext, roleContext, organizationId, Guid.NewGuid(), UserOrganizationPermissions.OrganizationMembersManage);

        var resolver = new OrganizationPermissionResolver(
            organizationContext,
            new StubRoleAssignmentService(Array.Empty<string>()),
            logger: null,
            roleContext);

        var permissions = await resolver.GetOrganizationPermissionsAsync(organizationId, userId);

        permissions.ShouldBeEmpty();
    }

    private static OrganizationDbContext CreateOrganizationDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrganizationDbContext(options);
    }

    private static IdentityRolesDbContext CreateRoleDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityRolesDbContext(options);
    }

    private static async Task SeedOrganizationGraphAsync(
        OrganizationDbContext organizationDbContext,
        IRoleDbContext roleDbContext,
        Guid organizationId,
        Guid userId,
        string permissionName)
    {
        var organization = new Organization
        {
            Id = organizationId,
            Slug = $"org-{organizationId:N}",
            DisplayName = "Test Org",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = OrganizationStatus.Active
        };

        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Managers",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = userId,
            IsPrimary = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var assignment = new OrganizationRoleAssignment
        {
            OrganizationId = organizationId,
            UserId = userId,
            RoleId = role.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var permission = await roleDbContext.Permissions
            .FirstOrDefaultAsync(entity => entity.Name == permissionName)
            .ConfigureAwait(false);

        if (permission is null)
        {
            permission = new Permission
            {
                Name = permissionName,
                Description = "Manage organization members"
            };
            roleDbContext.Permissions.Add(permission);
            await roleDbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        var rolePermission = new OrganizationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = permission.Id,
            OrganizationId = organizationId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        organizationDbContext.Organizations.Add(organization);
        organizationDbContext.OrganizationRoles.Add(role);
        organizationDbContext.OrganizationMemberships.Add(membership);
        organizationDbContext.OrganizationRoleAssignments.Add(assignment);
        organizationDbContext.OrganizationRolePermissions.Add(rolePermission);
        await organizationDbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private sealed class StubRoleAssignmentService : IRoleAssignmentService
    {
        private readonly IReadOnlyList<string> _permissions;

        public StubRoleAssignmentService(IReadOnlyList<string> permissions)
        {
            _permissions = permissions;
        }

        public Task AssignRolesAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetUserRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_permissions);
    }
}
