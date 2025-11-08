using Identity.Base.Roles.Abstractions;
using Identity.Base.Options;
using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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
        ConfigureTableNaming(modelBuilder);

        var customizationOptions = this.GetService<IDbContextOptions>()
                ?.FindExtension<IdentityBaseModelCustomizationOptionsExtension>()
                ?.Options
            ?? ((IInfrastructure<IServiceProvider>)this).Instance?.GetService<IdentityBaseModelCustomizationOptions>();
        if (customizationOptions is not null)
        {
            foreach (var configure in customizationOptions.IdentityRolesDbContextCustomizations)
            {
                configure(modelBuilder);
            }
        }
    }

    private void ConfigureTableNaming(ModelBuilder modelBuilder)
    {
        var prefix = IdentityDbNamingHelper.ResolveTablePrefix(this);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "RbacRoles"));
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "Permissions"));
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "RolePermissions"));
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "UserRolesRbac"));
            entity.HasIndex(ur => ur.RoleId)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "UserRolesRbac_RoleId"));
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "AuditEntries"));
        });
    }
}
