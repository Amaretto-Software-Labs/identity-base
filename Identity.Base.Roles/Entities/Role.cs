using System.Collections.Generic;

namespace Identity.Base.Roles.Entities;

public sealed class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
