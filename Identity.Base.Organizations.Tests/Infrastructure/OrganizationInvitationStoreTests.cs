using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Infrastructure;
using Identity.Base.Organizations.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Identity.Base.Organizations.Tests.Infrastructure;

public sealed class OrganizationInvitationStoreTests
{
    [Fact]
    public async Task CreateAsync_RejectsSecondInvitation_ForSameOrganizationAndEmail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new OrganizationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var store = new OrganizationInvitationStore(context);
        var organizationId = Guid.NewGuid();
        var first = CreateRecord(organizationId, "duplicate@example.com", DateTimeOffset.UtcNow.AddHours(1));
        var second = CreateRecord(organizationId, "duplicate@example.com", DateTimeOffset.UtcNow.AddHours(2));

        await store.CreateAsync(first);
        await Should.ThrowAsync<OrganizationInvitationAlreadyExistsException>(() => store.CreateAsync(second));

        (await context.OrganizationInvitations.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_ReplacesExpiredInvitation_ForSameOrganizationAndEmail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new OrganizationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var store = new OrganizationInvitationStore(context);
        var organizationId = Guid.NewGuid();
        var expired = CreateRecord(organizationId, "expired@example.com", DateTimeOffset.UtcNow.AddMinutes(-1));
        var replacement = CreateRecord(organizationId, "expired@example.com", DateTimeOffset.UtcNow.AddHours(1));

        await store.CreateAsync(expired);
        await store.CreateAsync(replacement);

        var stored = await context.OrganizationInvitations.SingleAsync();
        stored.Code.ShouldBe(replacement.Code);
    }

    private static OrganizationInvitationRecord CreateRecord(
        Guid organizationId,
        string email,
        DateTimeOffset expiresAtUtc)
        => new()
        {
            Code = Guid.NewGuid(),
            OrganizationId = organizationId,
            OrganizationSlug = "test-org",
            OrganizationName = "Test Org",
            Email = email,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };
}
