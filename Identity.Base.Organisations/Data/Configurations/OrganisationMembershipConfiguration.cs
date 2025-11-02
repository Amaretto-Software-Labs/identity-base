using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organisations.Data.Configurations;

public sealed class OrganisationMembershipConfiguration : IEntityTypeConfiguration<OrganisationMembership>
{
    public void Configure(EntityTypeBuilder<OrganisationMembership> builder)
    {
        builder.ToTable("Identity_OrganisationMemberships");

        builder.HasKey(membership => new { membership.OrganisationId, membership.UserId });

        builder.Property(membership => membership.IsPrimary)
            .HasDefaultValue(false);

        builder.Property(membership => membership.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(membership => membership.UpdatedAtUtc);

        builder.HasIndex(membership => new { membership.UserId, membership.TenantId })
            .HasDatabaseName("IX_OrganisationMemberships_User_Tenant");

        builder.HasIndex(membership => new { membership.OrganisationId, membership.CreatedAtUtc })
            .HasDatabaseName("IX_OrganisationMemberships_Organisation_Created");

        builder.HasIndex(membership => new { membership.OrganisationId, membership.UserId })
            .HasDatabaseName("IX_OrganisationMemberships_Organisation_User");

        builder.HasOne(membership => membership.Organisation)
            .WithMany(organisation => organisation.Memberships)
            .HasForeignKey(membership => membership.OrganisationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(membership => membership.RoleAssignments)
            .WithOne(assignment => assignment.Membership)
            .HasForeignKey(assignment => new { assignment.OrganisationId, assignment.UserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
