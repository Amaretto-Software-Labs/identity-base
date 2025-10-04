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

        organization.Slug.Should().Be("my-org");
        organization.DisplayName.Should().Be("Example Org");
        organization.Status.Should().Be(OrganizationStatus.Active);
        organization.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenSlugExists()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.CreateAsync(new OrganizationCreateRequest { Slug = "dup", DisplayName = "One" });

        await FluentActions.Invoking(() => service.CreateAsync(new OrganizationCreateRequest { Slug = "DUP", DisplayName = "Two" }))
            .Should().ThrowAsync<InvalidOperationException>();
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

        updated.DisplayName.Should().Be("Updated");
        updated.Metadata.Values.Should().ContainKey("plan").WhoseValue.Should().Be("pro");
        updated.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ArchiveAsync_SetsArchivedStatus()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var organization = await service.CreateAsync(new OrganizationCreateRequest { Slug = "archive", DisplayName = "Org" });
        await service.ArchiveAsync(organization.Id);

        var reloaded = await context.Organizations.FindAsync(organization.Id);
        reloaded!.Status.Should().Be(OrganizationStatus.Archived);
        reloaded.ArchivedAtUtc.Should().NotBeNull();
    }

    private static OrganizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrganizationDbContext(options);
    }

    private static OrganizationService CreateService(OrganizationDbContext context)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OrganizationOptions());
        return new OrganizationService(context, options, NullLogger<OrganizationService>.Instance);
    }
}
