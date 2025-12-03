using System;
using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

public sealed class OrganizationRolePermissionConfiguration : IEntityTypeConfiguration<OrganizationRolePermission>
{
    public void Configure(EntityTypeBuilder<OrganizationRolePermission> builder)
    {
        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.TenantId)
            .HasDefaultValue(Guid.Empty);

        builder.Property(permission => permission.OrganizationId)
            .HasDefaultValue(Guid.Empty);

        builder.Property(permission => permission.Id)
            .ValueGeneratedOnAdd();

        builder.Property(permission => permission.RoleId)
            .IsRequired();

        builder.Property(permission => permission.PermissionId)
            .IsRequired();

        builder.Property(permission => permission.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(permission => new { permission.RoleId, permission.PermissionId });

        builder.HasIndex(permission => new { permission.OrganizationId, permission.RoleId });

        builder.HasIndex(permission => new { permission.TenantId, permission.RoleId });

        builder.HasOne(permission => permission.Role)
            .WithMany(role => role.RolePermissions)
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
