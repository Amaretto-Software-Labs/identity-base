namespace Identity.Base.Roles.Entities;

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;
}
