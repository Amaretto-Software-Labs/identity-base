using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

public sealed class OrganizationRoleConfiguration : IEntityTypeConfiguration<OrganizationRole>
{
    public void Configure(EntityTypeBuilder<OrganizationRole> builder)
    {
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(role => role.Description)
            .HasMaxLength(512);

        builder.Property(role => role.IsSystemRole)
            .HasDefaultValue(false);

        builder.Property(role => role.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(role => role.UpdatedAtUtc);

        builder.HasIndex(role => new { role.TenantId, role.OrganizationId, role.Name })
            .IsUnique();

        builder.HasOne(role => role.Organization)
            .WithMany(organization => organization.Roles)
            .HasForeignKey(role => role.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(role => role.RoleAssignments)
            .WithOne(assignment => assignment.Role)
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(role => role.RolePermissions)
            .WithOne(permission => permission.Role)
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
