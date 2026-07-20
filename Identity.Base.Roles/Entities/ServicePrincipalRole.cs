namespace Identity.Base.Roles.Entities;

/// <summary>Assigns an existing RBAC role to a service principal.</summary>
public sealed class ServicePrincipalRole
{
    public Guid ServicePrincipalId { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
