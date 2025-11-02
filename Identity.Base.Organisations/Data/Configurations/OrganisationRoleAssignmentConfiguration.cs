using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organisations.Data.Configurations;

public sealed class OrganisationRoleAssignmentConfiguration : IEntityTypeConfiguration<OrganisationRoleAssignment>
{
    public void Configure(EntityTypeBuilder<OrganisationRoleAssignment> builder)
    {
        builder.ToTable("Identity_OrganisationRoleAssignments");

        builder.HasKey(assignment => new { assignment.OrganisationId, assignment.UserId, assignment.RoleId });

        builder.Property(assignment => assignment.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(assignment => assignment.UpdatedAtUtc);

        builder.HasIndex(assignment => assignment.RoleId)
            .HasDatabaseName("IX_OrganisationRoleAssignments_Role");

        builder.HasIndex(assignment => new { assignment.UserId, assignment.TenantId })
            .HasDatabaseName("IX_OrganisationRoleAssignments_User_Tenant");

        builder.HasOne(assignment => assignment.Organisation)
            .WithMany()
            .HasForeignKey(assignment => assignment.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(assignment => assignment.Role)
            .WithMany(role => role.RoleAssignments)
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
