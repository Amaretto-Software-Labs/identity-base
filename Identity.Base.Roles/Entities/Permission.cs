namespace Identity.Base.Roles.Entities;

public sealed class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
