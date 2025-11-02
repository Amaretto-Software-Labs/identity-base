using Shouldly;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Options;
using Identity.Base.Organisations.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organisations.Tests;

public class OrganisationServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesSlugAndDisplayName()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var organisation = await service.CreateAsync(new OrganisationCreateRequest
        {
            Slug = "  My-Org  ",
            DisplayName = "  Example Org  "
        });

        organisation.Slug.ShouldBe("my-org");
        organisation.DisplayName.ShouldBe("Example Org");
        organisation.Status.ShouldBe(OrganisationStatus.Active);
        var now = DateTimeOffset.UtcNow;
        organisation.CreatedAtUtc.ShouldBeInRange(now - TimeSpan.FromSeconds(5), now + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenSlugExists()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.CreateAsync(new OrganisationCreateRequest { Slug = "dup", DisplayName = "One" });

        await Should.ThrowAsync<InvalidOperationException>(() => service.CreateAsync(new OrganisationCreateRequest { Slug = "DUP", DisplayName = "Two" }));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDisplayNameAndMetadata()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var organisation = await service.CreateAsync(new OrganisationCreateRequest { Slug = "update", DisplayName = "Original" });

        var updated = await service.UpdateAsync(organisation.Id, new OrganisationUpdateRequest
        {
            DisplayName = "Updated",
            Metadata = new OrganisationMetadata(new Dictionary<string, string?> { ["plan"] = "pro" })
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

        var organisation = await service.CreateAsync(new OrganisationCreateRequest { Slug = "archive", DisplayName = "Org" });
        await service.ArchiveAsync(organisation.Id);

        var reloaded = await context.Organisations.FindAsync(organisation.Id);
        reloaded!.Status.ShouldBe(OrganisationStatus.Archived);
        reloaded.ArchivedAtUtc.ShouldNotBeNull();
    }

    private static OrganisationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganisationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrganisationDbContext(options);
    }

    private static OrganisationService CreateService(OrganisationDbContext context)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganisationOptions());
        return new OrganisationService(context, options, NullLogger<OrganisationService>.Instance);
    }
}
