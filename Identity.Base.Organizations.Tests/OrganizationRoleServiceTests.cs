using System.Linq;
using Shouldly;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles;
using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Tests;

public class OrganizationRoleServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsRole()
    {
        await using var context = CreateContext(out var organization);
        await using var roleContext = CreateRoleContext();
        var service = CreateService(context, roleContext);

        var role = await service.CreateAsync(new OrganizationRoleCreateRequest
        {
            OrganizationId = organization.Id,
            Name = "Manager"
        });

        role.OrganizationId.ShouldBe(organization.Id);
        role.Name.ShouldBe("Manager");
        var now = DateTimeOffset.UtcNow;
        role.CreatedAtUtc.ShouldBeInRange(now - TimeSpan.FromSeconds(5), now + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ListAsync_ReturnsRolesForOrganization()
    {
        await using var context = CreateContext(out var organization);
        await using var roleContext = CreateRoleContext();
        var service = CreateService(context, roleContext);

        await service.CreateAsync(new OrganizationRoleCreateRequest { OrganizationId = organization.Id, Name = "Owner" });
        await service.CreateAsync(new OrganizationRoleCreateRequest { OrganizationId = null, Name = "Shared" });

        var roles = await service.ListAsync(null, organization.Id);
        roles.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRole()
    {
        await using var context = CreateContext(out var organization);
        await using var roleContext = CreateRoleContext();
        var service = CreateService(context, roleContext);

        var role = await service.CreateAsync(new OrganizationRoleCreateRequest { OrganizationId = organization.Id, Name = "Temp" });
        await service.DeleteAsync(role.Id);

        (await context.OrganizationRoles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task GetPermissionsAsync_ReturnsExplicitAndInherited()
    {
        await using var context = CreateContext(out var organization);
        await using var roleContext = CreateRoleContext();

        var readPermission = new Permission { Name = "organization.roles.read" };
        var managePermission = new Permission { Name = "organization.roles.manage" };
        roleContext.Permissions.AddRange(readPermission, managePermission);
        await roleContext.SaveChangesAsync();

        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            Name = "OrgManager",
            IsSystemRole = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        context.OrganizationRoles.Add(role);
        await context.SaveChangesAsync();

        context.OrganizationRolePermissions.Add(new OrganizationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = readPermission.Id,
            OrganizationId = role.OrganizationId,
            TenantId = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        context.OrganizationRolePermissions.Add(new OrganizationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = managePermission.Id,
            OrganizationId = organization.Id,
            TenantId = organization.TenantId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, roleContext);
        var permissions = await service.GetPermissionsAsync(role.Id, organization.Id);

        permissions.Effective.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organization.roles.manage",
            "organization.roles.read",
        }.OrderBy(x => x).ToArray());

        permissions.Explicit.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organization.roles.manage",
        }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task UpdatePermissionsAsync_ReplacesExplicitAssignments()
    {
        await using var context = CreateContext(out var organization);
        await using var roleContext = CreateRoleContext();

        var readPermission = new Permission { Name = "organization.roles.read" };
        var managePermission = new Permission { Name = "organization.roles.manage" };
        var auditPermission = new Permission { Name = "organization.roles.audit" };
        roleContext.Permissions.AddRange(readPermission, managePermission, auditPermission);
        await roleContext.SaveChangesAsync();

        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            Name = "OrgOwner",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsSystemRole = true,
        };

        context.OrganizationRoles.Add(role);
        await context.SaveChangesAsync();

        context.OrganizationRolePermissions.Add(new OrganizationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = readPermission.Id,
            OrganizationId = role.OrganizationId,
            TenantId = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        context.OrganizationRolePermissions.Add(new OrganizationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = managePermission.Id,
            OrganizationId = organization.Id,
            TenantId = organization.TenantId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, roleContext);

        await service.UpdatePermissionsAsync(role.Id, organization.Id, new[] { "organization.roles.manage", "organization.roles.audit" });

        var afterUpdate = await service.GetPermissionsAsync(role.Id, organization.Id);

        afterUpdate.Effective.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organization.roles.audit",
            "organization.roles.manage",
            "organization.roles.read",
        }.OrderBy(x => x).ToArray());

        afterUpdate.Explicit.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organization.roles.audit",
            "organization.roles.manage",
        }.OrderBy(x => x).ToArray());

        await service.UpdatePermissionsAsync(role.Id, organization.Id, new[] { "organization.roles.audit" });

        var afterRemoval = await service.GetPermissionsAsync(role.Id, organization.Id);

        afterRemoval.Effective.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organization.roles.audit",
            "organization.roles.read",
        }.OrderBy(x => x).ToArray());

        afterRemoval.Explicit.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organization.roles.audit",
        }.OrderBy(x => x).ToArray());
    }

    private static OrganizationDbContext CreateContext(out Organization organization)
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new OrganizationDbContext(options);

        organization = new Organization
        {
            Id = Guid.NewGuid(),
            Slug = "org",
            DisplayName = "Org"
        };

        context.Organizations.Add(organization);
        context.SaveChanges();
        return context;
    }

    private static IdentityRolesDbContext CreateRoleContext()
    {
        var options = new DbContextOptionsBuilder<IdentityRolesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityRolesDbContext(options);
    }

    private static OrganizationRoleService CreateService(OrganizationDbContext context, IdentityRolesDbContext roleContext)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationRoleOptions());
        return new OrganizationRoleService(context, roleContext, options, NullLogger<OrganizationRoleService>.Instance);
    }
}
