using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("Identity_OrganizationMemberships");

        builder.HasKey(membership => new { membership.OrganizationId, membership.UserId });

        builder.Property(membership => membership.IsPrimary)
            .HasDefaultValue(false);

        builder.Property(membership => membership.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(membership => membership.UpdatedAtUtc);

        builder.HasIndex(membership => new { membership.UserId, membership.TenantId })
            .HasDatabaseName("IX_OrganizationMemberships_User_Tenant");

        builder.HasIndex(membership => new { membership.OrganizationId, membership.CreatedAtUtc })
            .HasDatabaseName("IX_OrganizationMemberships_Organization_Created");

        builder.HasIndex(membership => new { membership.OrganizationId, membership.UserId })
            .HasDatabaseName("IX_OrganizationMemberships_Organization_User");

        builder.HasOne(membership => membership.Organization)
            .WithMany(organization => organization.Memberships)
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(membership => membership.RoleAssignments)
            .WithOne(assignment => assignment.Membership)
            .HasForeignKey(assignment => new { assignment.OrganizationId, assignment.UserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
