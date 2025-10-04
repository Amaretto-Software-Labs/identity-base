namespace Identity.Base.Organizations.Api.Models;

public sealed class OrganizationMembershipDto
{
    public Guid OrganizationId { get; init; }

    public Guid UserId { get; init; }

    public Guid? TenantId { get; init; }

    public bool IsPrimary { get; init; }

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = Array.Empty<Guid>();

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }
}
