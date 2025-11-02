using Shouldly;
using Identity.Base.Data;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.Base.Organisations.Tests;

public class OrganisationMembershipServiceTests
{
    [Fact]
    public async Task AddMemberAsync_CreatesMembershipAndAssignments()
    {
        await using var context = CreateContext(out var appContext, out var organisation, out var role);
        await using var appDbContext = appContext;
        var service = new OrganisationMembershipService(context, appDbContext, NullLogger<OrganisationMembershipService>.Instance);

        var membership = await service.AddMemberAsync(new OrganisationMembershipRequest
        {
            OrganisationId = organisation.Id,
            UserId = Guid.NewGuid(),
            IsPrimary = true,
            RoleIds = new[] { role.Id }
        });

        membership.IsPrimary.ShouldBeTrue();
        var assignment = membership.RoleAssignments.ShouldHaveSingleItem();
        assignment.RoleId.ShouldBe(role.Id);

        var stored = await context.OrganisationMemberships.Include(m => m.RoleAssignments).FirstOrDefaultAsync();
        stored.ShouldNotBeNull();
        stored!.RoleAssignments.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AddMemberAsync_PreventsDuplicateMembership()
    {
        await using var context = CreateContext(out var appContext, out var organisation, out _);
        await using var appDbContext = appContext;
        var service = new OrganisationMembershipService(context, appDbContext, NullLogger<OrganisationMembershipService>.Instance);
        var userId = Guid.NewGuid();

        await service.AddMemberAsync(new OrganisationMembershipRequest
        {
            OrganisationId = organisation.Id,
            UserId = userId
        });

        await Should.ThrowAsync<InvalidOperationException>(() => service.AddMemberAsync(new OrganisationMembershipRequest
        {
            OrganisationId = organisation.Id,
            UserId = userId
        }));
    }

    [Fact]
    public async Task UpdateMembershipAsync_SetsPrimaryAndUpdatesRoles()
    {
        await using var context = CreateContext(out var appContext, out var organisation, out var role);
        await using var appDbContext = appContext;
        var service = new OrganisationMembershipService(context, appDbContext, NullLogger<OrganisationMembershipService>.Instance);
        var userId = Guid.NewGuid();

        await service.AddMemberAsync(new OrganisationMembershipRequest
        {
            OrganisationId = organisation.Id,
            UserId = userId
        });

        var updated = await service.UpdateMembershipAsync(new OrganisationMembershipUpdateRequest
        {
            OrganisationId = organisation.Id,
            UserId = userId,
            IsPrimary = true,
            RoleIds = new[] { role.Id }
        });

        updated.IsPrimary.ShouldBeTrue();
        var updatedAssignment = updated.RoleAssignments.ShouldHaveSingleItem();
        updatedAssignment.RoleId.ShouldBe(role.Id);
    }

    [Fact]
    public async Task RemoveMemberAsync_DeletesMembership()
    {
        await using var context = CreateContext(out var appContext, out var organisation, out _);
        await using var appDbContext = appContext;
        var service = new OrganisationMembershipService(context, appDbContext, NullLogger<OrganisationMembershipService>.Instance);
        var userId = Guid.NewGuid();

        await service.AddMemberAsync(new OrganisationMembershipRequest
        {
            OrganisationId = organisation.Id,
            UserId = userId
        });

        await service.RemoveMemberAsync(organisation.Id, userId);

        (await context.OrganisationMemberships.CountAsync()).ShouldBe(0);
    }

    private static OrganisationDbContext CreateContext(out AppDbContext appContext, out Organisation organisation, out OrganisationRole role)
    {
        var options = new DbContextOptionsBuilder<OrganisationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new OrganisationDbContext(options);

        var appOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        appContext = new AppDbContext(appOptions);

        organisation = new Organisation
        {
            Id = Guid.NewGuid(),
            Slug = "org",
            DisplayName = "Org"
        };

        role = new OrganisationRole
        {
            Id = Guid.NewGuid(),
            Name = "Member"
        };

        context.Organisations.Add(organisation);
        context.OrganisationRoles.Add(role);
        context.SaveChanges();
        return context;
    }
}
