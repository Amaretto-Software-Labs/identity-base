using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

public sealed class OrganizationRolePermissionConfiguration : IEntityTypeConfiguration<OrganizationRolePermission>
{
    public void Configure(EntityTypeBuilder<OrganizationRolePermission> builder)
    {
        builder.ToTable("Identity_OrganizationRolePermissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(permission => permission.RoleId)
            .IsRequired();

        builder.Property(permission => permission.PermissionId)
            .IsRequired();

        builder.Property(permission => permission.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(permission => new { permission.RoleId, permission.PermissionId })
            .HasDatabaseName("IX_OrganizationRolePermissions_Role_Permission");

        builder.HasIndex(permission => new { permission.OrganizationId, permission.RoleId })
            .HasDatabaseName("IX_OrganizationRolePermissions_Organization_Role");

        builder.HasIndex(permission => new { permission.TenantId, permission.RoleId })
            .HasDatabaseName("IX_OrganizationRolePermissions_Tenant_Role");

        builder.HasOne(permission => permission.Role)
            .WithMany(role => role.RolePermissions)
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
