using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

public sealed class OrganizationRoleAssignmentConfiguration : IEntityTypeConfiguration<OrganizationRoleAssignment>
{
    public void Configure(EntityTypeBuilder<OrganizationRoleAssignment> builder)
    {
        builder.HasKey(assignment => new { assignment.OrganizationId, assignment.UserId, assignment.RoleId });

        builder.Property(assignment => assignment.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(assignment => assignment.UpdatedAtUtc);

        builder.HasIndex(assignment => assignment.RoleId);

        builder.HasIndex(assignment => new { assignment.UserId, assignment.TenantId });

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
