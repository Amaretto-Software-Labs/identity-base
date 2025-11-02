using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Api.Models;

public sealed class OrganizationDto
{
    public Guid Id { get; init; }

    public Guid? TenantId { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public OrganizationStatus Status { get; init; }

    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>();

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }

    public DateTimeOffset? ArchivedAtUtc { get; init; }
}
