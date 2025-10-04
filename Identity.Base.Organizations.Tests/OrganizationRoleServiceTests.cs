using FluentAssertions;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
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
        var service = CreateService(context);

        var role = await service.CreateAsync(new OrganizationRoleCreateRequest
        {
            OrganizationId = organization.Id,
            Name = "Manager"
        });

        role.OrganizationId.Should().Be(organization.Id);
        role.Name.Should().Be("Manager");
        role.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ListAsync_ReturnsRolesForOrganization()
    {
        await using var context = CreateContext(out var organization);
        var service = CreateService(context);

        await service.CreateAsync(new OrganizationRoleCreateRequest { OrganizationId = organization.Id, Name = "Owner" });
        await service.CreateAsync(new OrganizationRoleCreateRequest { OrganizationId = null, Name = "Shared" });

        var roles = await service.ListAsync(null, organization.Id);
        roles.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRole()
    {
        await using var context = CreateContext(out var organization);
        var service = CreateService(context);

        var role = await service.CreateAsync(new OrganizationRoleCreateRequest { OrganizationId = organization.Id, Name = "Temp" });
        await service.DeleteAsync(role.Id);

        (await context.OrganizationRoles.CountAsync()).Should().Be(0);
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

    private static OrganizationRoleService CreateService(OrganizationDbContext context)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationRoleOptions());
        return new OrganizationRoleService(context, options, NullLogger<OrganizationRoleService>.Instance);
    }
}
