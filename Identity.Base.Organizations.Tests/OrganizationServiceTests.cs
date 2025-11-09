using Shouldly;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        IEnumerable<IOrganizationCreationListener>? creationListeners = null,
        IEnumerable<IOrganizationUpdateListener>? updateListeners = null,
        IEnumerable<IOrganizationArchiveListener>? archiveListeners = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationOptions());
        return new OrganizationService(
            context,
            options,
            NullLogger<OrganizationService>.Instance,
            creationListeners ?? Array.Empty<IOrganizationCreationListener>(),
            updateListeners ?? Array.Empty<IOrganizationUpdateListener>(),
            archiveListeners ?? Array.Empty<IOrganizationArchiveListener>());
    }

    [Fact]
    public async Task CreateAsync_InvokesCreationListeners()
    {
        await using var context = CreateContext();
        var listener = new TestCreationListener();
        var service = CreateService(context, new[] { listener });

        var organization = await service.CreateAsync(new OrganizationCreateRequest
        {
            Slug = "listener-org",
            DisplayName = "Listener Org"
        });

        listener.Created.ShouldContain(organization.Id);
    }

    [Fact]
    public async Task UpdateAsync_InvokesUpdateListeners()
    {
        await using var context = CreateContext();
        var listener = new TestUpdateListener();
        var service = CreateService(context, updateListeners: new[] { listener });

        var organization = await service.CreateAsync(new OrganizationCreateRequest { Slug = "u-listener", DisplayName = "Before" });
        await service.UpdateAsync(organization.Id, new OrganizationUpdateRequest { DisplayName = "After" });

        listener.Updated.ShouldContain(organization.Id);
    }

    [Fact]
    public async Task ArchiveAsync_InvokesArchiveListeners()
    {
        await using var context = CreateContext();
        var listener = new TestArchiveListener();
        var service = CreateService(context, archiveListeners: new[] { listener });

        var organization = await service.CreateAsync(new OrganizationCreateRequest { Slug = "a-listener", DisplayName = "Org" });
        await service.ArchiveAsync(organization.Id);

        listener.Archived.ShouldContain(organization.Id);
    }

    private sealed class TestCreationListener : IOrganizationCreationListener
    {
        public List<Guid> Created { get; } = new();

        public Task OnOrganizationCreatedAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            Created.Add(organization.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class TestUpdateListener : IOrganizationUpdateListener
    {
        public List<Guid> Updated { get; } = new();

        public Task OnOrganizationUpdatedAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            Updated.Add(organization.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class TestArchiveListener : IOrganizationArchiveListener
    {
        public List<Guid> Archived { get; } = new();

        public Task OnOrganizationArchivedAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            Archived.Add(organization.Id);
            return Task.CompletedTask;
        }
    }
}
