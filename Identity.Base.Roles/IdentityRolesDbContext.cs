using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Roles;

public class IdentityRolesDbContext(DbContextOptions<IdentityRolesDbContext> options)
    : DbContext(options), IRoleDbContext
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityRolesDbContext).Assembly);
    }
}
