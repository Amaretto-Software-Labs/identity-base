using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

public sealed class OrganizationRoleAssignmentConfiguration : IEntityTypeConfiguration<OrganizationRoleAssignment>
{
    public void Configure(EntityTypeBuilder<OrganizationRoleAssignment> builder)
    {
        builder.ToTable("Identity_OrganizationRoleAssignments");

        builder.HasKey(assignment => new { assignment.OrganizationId, assignment.UserId, assignment.RoleId });

        builder.Property(assignment => assignment.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(assignment => assignment.UpdatedAtUtc);

        builder.HasIndex(assignment => assignment.RoleId)
            .HasDatabaseName("IX_OrganizationRoleAssignments_Role");

        builder.HasIndex(assignment => new { assignment.UserId, assignment.TenantId })
            .HasDatabaseName("IX_OrganizationRoleAssignments_User_Tenant");

        builder.HasOne(assignment => assignment.Organization)
            .WithMany()
            .HasForeignKey(assignment => assignment.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(assignment => assignment.Role)
            .WithMany(role => role.RoleAssignments)
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
