namespace Identity.Base.Organisations.Api.Models;

public sealed class AddMembershipRequest
{
    public Guid UserId { get; init; }

    public bool IsPrimary { get; init; }

    public IReadOnlyCollection<Guid> RoleIds { get; init; } = Array.Empty<Guid>();
}
