namespace Identity.Base.Organizations.Api.Models;

public sealed class UpdateMembershipRequest
{
    public IReadOnlyCollection<Guid>? RoleIds { get; init; }
}
