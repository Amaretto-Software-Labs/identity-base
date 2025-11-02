using System.Linq;
using Shouldly;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Services;
using Identity.Base.Roles;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Identity.Base.Roles.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organisations.Tests.Services;

public class OrganisationPermissionResolverTests
{
    [Fact]
    public async Task GetPermissionsAsync_ReturnsGlobalAndOrganisationPermissions()
    {
        await using var organisationContext = CreateOrganisationDbContext();
        await using var roleContext = CreateRoleDbContext();

        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permissionName = "organisation.members.manage";

        await SeedOrganisationGraphAsync(organisationContext, roleContext, organisationId, userId, permissionName);

        var resolver = new OrganisationPermissionResolver(
            organisationContext,
            new StubRoleAssignmentService(["users.read"]),
            logger: null,
            roleContext);

        var permissions = await resolver.GetPermissionsAsync(organisationId, userId);

        permissions.OrderBy(x => x).ToArray()
            .ShouldBe(new[] { "users.read", permissionName }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task GetOrganisationPermissionsAsync_ReturnsEmpty_WhenMembershipMissing()
    {
        await using var organisationContext = CreateOrganisationDbContext();
        await using var roleContext = CreateRoleDbContext();

        var organisationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedOrganisationGraphAsync(organisationContext, roleContext, organisationId, Guid.NewGuid(), "organisation.members.manage");

        var resolver = new OrganisationPermissionResolver(
            organisationContext,
            new StubRoleAssignmentService(Array.Empty<string>()),
            logger: null,
            roleContext);

        var permissions = await resolver.GetOrganisationPermissionsAsync(organisationId, userId);

        permissions.ShouldBeEmpty();
    }

    private static OrganisationDbContext CreateOrganisationDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganisationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrganisationDbContext(options);
    }

    private static IdentityRolesDbContext CreateRoleDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityRolesDbContext(options);
    }

    private static async Task SeedOrganisationGraphAsync(
        OrganisationDbContext organisationDbContext,
        IRoleDbContext roleDbContext,
        Guid organisationId,
        Guid userId,
        string permissionName)
    {
        var organisation = new Organisation
        {
            Id = organisationId,
            Slug = $"org-{organisationId:N}",
            DisplayName = "Test Org",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = OrganisationStatus.Active
        };

        var role = new OrganisationRole
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            Name = "Managers",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var membership = new OrganisationMembership
        {
            OrganisationId = organisationId,
            UserId = userId,
            IsPrimary = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var assignment = new OrganisationRoleAssignment
        {
            OrganisationId = organisationId,
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
                Description = "Manage organisation members"
            };
            roleDbContext.Permissions.Add(permission);
            await roleDbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        var rolePermission = new OrganisationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = permission.Id,
            OrganisationId = organisationId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        organisationDbContext.Organisations.Add(organisation);
        organisationDbContext.OrganisationRoles.Add(role);
        organisationDbContext.OrganisationMemberships.Add(membership);
        organisationDbContext.OrganisationRoleAssignments.Add(assignment);
        organisationDbContext.OrganisationRolePermissions.Add(rolePermission);
        await organisationDbContext.SaveChangesAsync().ConfigureAwait(false);
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
