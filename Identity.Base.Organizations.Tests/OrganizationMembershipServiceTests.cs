using FluentAssertions;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.Base.Organizations.Tests;

public class OrganizationMembershipServiceTests
{
    [Fact]
    public async Task AddMemberAsync_CreatesMembershipAndAssignments()
    {
        await using var context = CreateContext(out var organization, out var role);
        var service = new OrganizationMembershipService(context, NullLogger<OrganizationMembershipService>.Instance);

        var membership = await service.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organization.Id,
            UserId = Guid.NewGuid(),
            IsPrimary = true,
            RoleIds = new[] { role.Id }
        });

        membership.IsPrimary.Should().BeTrue();
        membership.RoleAssignments.Should().ContainSingle().Which.RoleId.Should().Be(role.Id);

        var stored = await context.OrganizationMemberships.Include(m => m.RoleAssignments).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.RoleAssignments.Should().ContainSingle();
    }

    [Fact]
    public async Task AddMemberAsync_PreventsDuplicateMembership()
    {
        await using var context = CreateContext(out var organization, out _);
        var service = new OrganizationMembershipService(context, NullLogger<OrganizationMembershipService>.Instance);
        var userId = Guid.NewGuid();

        await service.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organization.Id,
            UserId = userId
        });

        await FluentActions.Invoking(() => service.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organization.Id,
            UserId = userId
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateMembershipAsync_SetsPrimaryAndUpdatesRoles()
    {
        await using var context = CreateContext(out var organization, out var role);
        var service = new OrganizationMembershipService(context, NullLogger<OrganizationMembershipService>.Instance);
        var userId = Guid.NewGuid();

        await service.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organization.Id,
            UserId = userId
        });

        var updated = await service.UpdateMembershipAsync(new OrganizationMembershipUpdateRequest
        {
            OrganizationId = organization.Id,
            UserId = userId,
            IsPrimary = true,
            RoleIds = new[] { role.Id }
        });

        updated.IsPrimary.Should().BeTrue();
        updated.RoleAssignments.Should().ContainSingle().Which.RoleId.Should().Be(role.Id);
    }

    [Fact]
    public async Task RemoveMemberAsync_DeletesMembership()
    {
        await using var context = CreateContext(out var organization, out _);
        var service = new OrganizationMembershipService(context, NullLogger<OrganizationMembershipService>.Instance);
        var userId = Guid.NewGuid();

        await service.AddMemberAsync(new OrganizationMembershipRequest
        {
            OrganizationId = organization.Id,
            UserId = userId
        });

        await service.RemoveMemberAsync(organization.Id, userId);

        (await context.OrganizationMemberships.CountAsync()).Should().Be(0);
    }

    private static OrganizationDbContext CreateContext(out Organization organization, out OrganizationRole role)
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

        role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            Name = "Member"
        };

        context.Organizations.Add(organization);
        context.OrganizationRoles.Add(role);
        context.SaveChanges();
        return context;
    }
}
