using System.Linq;
using Shouldly;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Options;
using Identity.Base.Organisations.Services;
using Identity.Base.Roles;
using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organisations.Tests;

public class OrganisationRoleServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsRole()
    {
        await using var context = CreateContext(out var organisation);
        await using var roleContext = CreateRoleContext();
        var service = CreateService(context, roleContext);

        var role = await service.CreateAsync(new OrganisationRoleCreateRequest
        {
            OrganisationId = organisation.Id,
            Name = "Manager"
        });

        role.OrganisationId.ShouldBe(organisation.Id);
        role.Name.ShouldBe("Manager");
        var now = DateTimeOffset.UtcNow;
        role.CreatedAtUtc.ShouldBeInRange(now - TimeSpan.FromSeconds(5), now + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ListAsync_ReturnsRolesForOrganisation()
    {
        await using var context = CreateContext(out var organisation);
        await using var roleContext = CreateRoleContext();
        var service = CreateService(context, roleContext);

        await service.CreateAsync(new OrganisationRoleCreateRequest { OrganisationId = organisation.Id, Name = "Owner" });
        await service.CreateAsync(new OrganisationRoleCreateRequest { OrganisationId = null, Name = "Shared" });

        var roles = await service.ListAsync(null, organisation.Id);
        roles.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRole()
    {
        await using var context = CreateContext(out var organisation);
        await using var roleContext = CreateRoleContext();
        var service = CreateService(context, roleContext);

        var role = await service.CreateAsync(new OrganisationRoleCreateRequest { OrganisationId = organisation.Id, Name = "Temp" });
        await service.DeleteAsync(role.Id);

        (await context.OrganisationRoles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task GetPermissionsAsync_ReturnsExplicitAndInherited()
    {
        await using var context = CreateContext(out var organisation);
        await using var roleContext = CreateRoleContext();

        var readPermission = new Permission { Name = "organisation.roles.read" };
        var managePermission = new Permission { Name = "organisation.roles.manage" };
        roleContext.Permissions.AddRange(readPermission, managePermission);
        await roleContext.SaveChangesAsync();

        var role = new OrganisationRole
        {
            Id = Guid.NewGuid(),
            Name = "OrgManager",
            IsSystemRole = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        context.OrganisationRoles.Add(role);
        await context.SaveChangesAsync();

        context.OrganisationRolePermissions.Add(new OrganisationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = readPermission.Id,
            OrganisationId = role.OrganisationId,
            TenantId = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        context.OrganisationRolePermissions.Add(new OrganisationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = managePermission.Id,
            OrganisationId = organisation.Id,
            TenantId = organisation.TenantId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, roleContext);
        var permissions = await service.GetPermissionsAsync(role.Id, organisation.Id);

        permissions.Effective.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organisation.roles.manage",
            "organisation.roles.read",
        }.OrderBy(x => x).ToArray());

        permissions.Explicit.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organisation.roles.manage",
        }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task UpdatePermissionsAsync_ReplacesExplicitAssignments()
    {
        await using var context = CreateContext(out var organisation);
        await using var roleContext = CreateRoleContext();

        var readPermission = new Permission { Name = "organisation.roles.read" };
        var managePermission = new Permission { Name = "organisation.roles.manage" };
        var auditPermission = new Permission { Name = "organisation.roles.audit" };
        roleContext.Permissions.AddRange(readPermission, managePermission, auditPermission);
        await roleContext.SaveChangesAsync();

        var role = new OrganisationRole
        {
            Id = Guid.NewGuid(),
            Name = "OrgOwner",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsSystemRole = true,
        };

        context.OrganisationRoles.Add(role);
        await context.SaveChangesAsync();

        context.OrganisationRolePermissions.Add(new OrganisationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = readPermission.Id,
            OrganisationId = role.OrganisationId,
            TenantId = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        context.OrganisationRolePermissions.Add(new OrganisationRolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            PermissionId = managePermission.Id,
            OrganisationId = organisation.Id,
            TenantId = organisation.TenantId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, roleContext);

        await service.UpdatePermissionsAsync(role.Id, organisation.Id, new[] { "organisation.roles.manage", "organisation.roles.audit" });

        var afterUpdate = await service.GetPermissionsAsync(role.Id, organisation.Id);

        afterUpdate.Effective.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organisation.roles.audit",
            "organisation.roles.manage",
            "organisation.roles.read",
        }.OrderBy(x => x).ToArray());

        afterUpdate.Explicit.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organisation.roles.audit",
            "organisation.roles.manage",
        }.OrderBy(x => x).ToArray());

        await service.UpdatePermissionsAsync(role.Id, organisation.Id, new[] { "organisation.roles.audit" });

        var afterRemoval = await service.GetPermissionsAsync(role.Id, organisation.Id);

        afterRemoval.Effective.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organisation.roles.audit",
            "organisation.roles.read",
        }.OrderBy(x => x).ToArray());

        afterRemoval.Explicit.OrderBy(x => x).ToArray().ShouldBe(new[]
        {
            "organisation.roles.audit",
        }.OrderBy(x => x).ToArray());
    }

    private static OrganisationDbContext CreateContext(out Organisation organisation)
    {
        var options = new DbContextOptionsBuilder<OrganisationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new OrganisationDbContext(options);

        organisation = new Organisation
        {
            Id = Guid.NewGuid(),
            Slug = "org",
            DisplayName = "Org"
        };

        context.Organisations.Add(organisation);
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

    private static OrganisationRoleService CreateService(OrganisationDbContext context, IdentityRolesDbContext roleContext)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganisationRoleOptions());
        return new OrganisationRoleService(context, roleContext, options, NullLogger<OrganisationRoleService>.Instance);
    }
}
