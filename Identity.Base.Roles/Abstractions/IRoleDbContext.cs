using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Identity.Base.Roles.Abstractions;

public interface IRoleDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    DatabaseFacade Database { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
