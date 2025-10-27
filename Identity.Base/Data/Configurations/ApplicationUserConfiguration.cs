using Identity.Base.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Identity_Users");

        builder.Property(user => user.DisplayName)
            .HasMaxLength(ApplicationUser.DisplayNameMaxLength);

        builder.Property(user => user.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAdd();

        builder.Property(user => user.ProfileMetadata)
            .HasConversion(
                metadata => metadata.ToJson(),
                json => UserProfileMetadata.FromJson(json))
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.HasIndex(user => user.Email)
            .HasDatabaseName("IX_Identity_Users_Email");

        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("IX_Identity_Users_NormalizedEmail")
            .IsUnique()
            .HasFilter("\"NormalizedEmail\" IS NOT NULL");

        builder.HasIndex(user => user.NormalizedUserName)
            .HasDatabaseName("IX_Identity_Users_NormalizedUserName")
            .IsUnique()
            .HasFilter("\"NormalizedUserName\" IS NOT NULL");

        builder.HasIndex(user => user.CreatedAt)
            .HasDatabaseName("IX_Identity_Users_CreatedAt");
    }
}
