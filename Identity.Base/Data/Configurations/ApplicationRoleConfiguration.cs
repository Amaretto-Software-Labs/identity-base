using Identity.Base.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Data.Configurations;

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Identity_Roles");

        builder.Property(role => role.Name)
            .HasMaxLength(256);

        builder.Property(role => role.NormalizedName)
            .HasMaxLength(256);

        builder.HasIndex(role => role.NormalizedName)
            .HasDatabaseName("IX_Identity_Roles_NormalizedName")
            .IsUnique()
            .HasFilter("\"NormalizedName\" IS NOT NULL");
    }
}
