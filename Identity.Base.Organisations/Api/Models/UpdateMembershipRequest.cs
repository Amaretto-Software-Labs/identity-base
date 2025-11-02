namespace Identity.Base.Organisations.Api.Models;

public sealed class UpdateMembershipRequest
{
    public bool? IsPrimary { get; init; }

    public IReadOnlyCollection<Guid>? RoleIds { get; init; }
}
