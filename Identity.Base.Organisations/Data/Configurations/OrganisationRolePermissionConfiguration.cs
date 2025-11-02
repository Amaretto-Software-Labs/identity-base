using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organisations.Data.Configurations;

public sealed class OrganisationRolePermissionConfiguration : IEntityTypeConfiguration<OrganisationRolePermission>
{
    public void Configure(EntityTypeBuilder<OrganisationRolePermission> builder)
    {
        builder.ToTable("Identity_OrganisationRolePermissions");

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
            .HasDatabaseName("IX_OrganisationRolePermissions_Role_Permission");

        builder.HasIndex(permission => new { permission.OrganisationId, permission.RoleId })
            .HasDatabaseName("IX_OrganisationRolePermissions_Organisation_Role");

        builder.HasIndex(permission => new { permission.TenantId, permission.RoleId })
            .HasDatabaseName("IX_OrganisationRolePermissions_Tenant_Role");

        builder.HasOne(permission => permission.Role)
            .WithMany(role => role.RolePermissions)
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
