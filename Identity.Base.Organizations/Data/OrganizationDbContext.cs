using Identity.Base.Options;
using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Organizations.Data;

public class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();

    public DbSet<OrganizationRoleAssignment> OrganizationRoleAssignments => Set<OrganizationRoleAssignment>();

    public DbSet<OrganizationRolePermission> OrganizationRolePermissions => Set<OrganizationRolePermission>();

    public DbSet<Entities.OrganizationInvitationEntity> OrganizationInvitations => Set<Entities.OrganizationInvitationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
        ConfigureTableNaming(modelBuilder);

        var customizationOptions = this.GetService<IDbContextOptions>()
                ?.FindExtension<IdentityBaseModelCustomizationOptionsExtension>()
                ?.Options
            ?? ((IInfrastructure<IServiceProvider>)this).Instance?.GetService<IdentityBaseModelCustomizationOptions>();
        if (customizationOptions is null)
        {
            return;
        }

        foreach (var configure in customizationOptions.OrganizationDbContextCustomizations)
        {
            configure(modelBuilder);
        }
    }

    private void ConfigureTableNaming(ModelBuilder modelBuilder)
    {
        var prefix = IdentityDbNamingHelper.ResolveTablePrefix(this);

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "Organizations"));
            entity.HasIndex(organization => new { organization.TenantId, organization.Slug })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Organizations_Tenant_Slug"))
                .IsUnique();
            entity.HasIndex(organization => new { organization.TenantId, organization.DisplayName })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Organizations_Tenant_DisplayName"))
                .IsUnique();
        });

        modelBuilder.Entity<OrganizationMembership>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "OrganizationMemberships"));
            entity.HasIndex(membership => new { membership.UserId, membership.TenantId })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationMemberships_User_Tenant"));
            entity.HasIndex(membership => new { membership.OrganizationId, membership.CreatedAtUtc })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationMemberships_Organization_Created"));
            entity.HasIndex(membership => new { membership.OrganizationId, membership.UserId })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationMemberships_Organization_User"));
        });

        modelBuilder.Entity<OrganizationRole>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "OrganizationRoles"));
            entity.HasIndex(role => new { role.TenantId, role.OrganizationId, role.Name })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationRoles_Tenant_Organization_Name"))
                .IsUnique();
        });

        modelBuilder.Entity<OrganizationRoleAssignment>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "OrganizationRoleAssignments"));
            entity.HasIndex(assignment => assignment.RoleId)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationRoleAssignments_Role"));
            entity.HasIndex(assignment => new { assignment.UserId, assignment.TenantId })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationRoleAssignments_User_Tenant"));
        });

        modelBuilder.Entity<OrganizationRolePermission>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "OrganizationRolePermissions"));
            entity.HasIndex(permission => new { permission.RoleId, permission.PermissionId })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationRolePermissions_Role_Permission"));
            entity.HasIndex(permission => new { permission.OrganizationId, permission.RoleId })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationRolePermissions_Organization_Role"));
            entity.HasIndex(permission => new { permission.TenantId, permission.RoleId })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationRolePermissions_Tenant_Role"));
        });

        modelBuilder.Entity<Entities.OrganizationInvitationEntity>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "OrganizationInvitations"));
            entity.HasKey(invitation => invitation.Code)
                .HasName(IdentityDbNamingHelper.PrimaryKey(prefix, "OrganizationInvitations"));
            entity.HasIndex(invitation => invitation.OrganizationId)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationInvitations_OrganizationId"));
            entity.HasIndex(invitation => invitation.Email)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationInvitations_Email"));
            entity.HasIndex(invitation => invitation.UsedAtUtc)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "OrganizationInvitations_UsedAtUtc"));
        });
    }
}
