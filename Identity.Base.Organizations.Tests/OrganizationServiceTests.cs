using Shouldly;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Identity.Base.Organizations.Lifecycle;

namespace Identity.Base.Organizations.Tests;

public class OrganizationServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesSlugAndDisplayName()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var organization = await service.CreateAsync(new OrganizationCreateRequest
        {
            Slug = "  My-Org  ",
            DisplayName = "  Example Org  "
        });

        organization.Slug.ShouldBe("my-org");
        organization.DisplayName.ShouldBe("Example Org");
        organization.Status.ShouldBe(OrganizationStatus.Active);
        var now = DateTimeOffset.UtcNow;
        organization.CreatedAtUtc.ShouldBeInRange(now - TimeSpan.FromSeconds(5), now + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenSlugExists()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.CreateAsync(new OrganizationCreateRequest { Slug = "dup", DisplayName = "One" });

        await Should.ThrowAsync<InvalidOperationException>(() => service.CreateAsync(new OrganizationCreateRequest { Slug = "DUP", DisplayName = "Two" }));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDisplayNameAndMetadata()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var organization = await service.CreateAsync(new OrganizationCreateRequest { Slug = "update", DisplayName = "Original" });

        var updated = await service.UpdateAsync(organization.Id, new OrganizationUpdateRequest
        {
            DisplayName = "Updated",
            Metadata = new OrganizationMetadata(new Dictionary<string, string?> { ["plan"] = "pro" })
        });

        updated.DisplayName.ShouldBe("Updated");
        updated.Metadata.Values.ShouldContainKey("plan");
        updated.Metadata.Values["plan"].ShouldBe("pro");
        updated.UpdatedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ArchiveAsync_SetsArchivedStatus()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var organization = await service.CreateAsync(new OrganizationCreateRequest { Slug = "archive", DisplayName = "Org" });
        await service.ArchiveAsync(organization.Id);

        var reloaded = await context.Organizations.FindAsync(organization.Id);
        reloaded!.Status.ShouldBe(OrganizationStatus.Archived);
        reloaded.ArchivedAtUtc.ShouldNotBeNull();
    }

    private static OrganizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrganizationDbContext(options);
    }

    private static OrganizationService CreateService(
        OrganizationDbContext context,
        IOrganizationLifecycleHookDispatcher? lifecycleDispatcher = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationOptions());
        return new OrganizationService(
            context,
            options,
            NullLogger<OrganizationService>.Instance,
            lifecycleDispatcher ?? NullOrganizationLifecycleDispatcher.Instance);
    }

    [Fact]
    public async Task CreateAsync_EmitsLifecycleEvents()
    {
        await using var context = CreateContext();
        var dispatcher = new TestLifecycleDispatcher();
        var service = CreateService(context, dispatcher);

        await service.CreateAsync(new OrganizationCreateRequest
        {
            Slug = "listener-org",
            DisplayName = "Listener Org"
        });

        dispatcher.Events.ShouldContain(OrganizationLifecycleEvent.OrganizationCreated);
    }

    [Fact]
    public async Task UpdateAsync_EmitsLifecycleEvents()
    {
        await using var context = CreateContext();
        var dispatcher = new TestLifecycleDispatcher();
        var service = CreateService(context, dispatcher);

        var organization = await service.CreateAsync(new OrganizationCreateRequest { Slug = "u-listener", DisplayName = "Before" });
        await service.UpdateAsync(organization.Id, new OrganizationUpdateRequest { DisplayName = "After" });

        dispatcher.Events.ShouldContain(OrganizationLifecycleEvent.OrganizationUpdated);
    }

    [Fact]
    public async Task ArchiveAsync_EmitsLifecycleEvents()
    {
        await using var context = CreateContext();
        var dispatcher = new TestLifecycleDispatcher();
        var service = CreateService(context, dispatcher);

        var organization = await service.CreateAsync(new OrganizationCreateRequest { Slug = "a-listener", DisplayName = "Org" });
        await service.ArchiveAsync(organization.Id);

        dispatcher.Events.ShouldContain(OrganizationLifecycleEvent.OrganizationArchived);
    }

    private sealed class TestLifecycleDispatcher : IOrganizationLifecycleHookDispatcher
    {
        public List<OrganizationLifecycleEvent> Events { get; } = new();

        public Task EnsureCanCreateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyOrganizationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanUpdateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyOrganizationUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanArchiveOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyOrganizationArchivedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanRestoreOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyOrganizationRestoredAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanCreateInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanRevokeInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanAddMemberAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanUpdateMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyMembershipUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanRevokeMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }

        public Task EnsureCanAcceptInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyInvitationAcceptedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        {
            Events.Add(context.Event);
            return Task.CompletedTask;
        }
    }
}
