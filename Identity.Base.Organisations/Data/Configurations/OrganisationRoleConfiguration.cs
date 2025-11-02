using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organisations.Data.Configurations;

public sealed class OrganisationRoleConfiguration : IEntityTypeConfiguration<OrganisationRole>
{
    public void Configure(EntityTypeBuilder<OrganisationRole> builder)
    {
        builder.ToTable("Identity_OrganisationRoles");

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

        builder.HasIndex(role => new { role.TenantId, role.OrganisationId, role.Name })
            .IsUnique()
            .HasDatabaseName("IX_OrganisationRoles_Tenant_Organisation_Name");

        builder.HasOne(role => role.Organisation)
            .WithMany(organisation => organisation.Roles)
            .HasForeignKey(role => role.OrganisationId)
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
