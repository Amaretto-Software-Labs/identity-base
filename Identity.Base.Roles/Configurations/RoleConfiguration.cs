using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Roles.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(role => role.Description)
            .HasMaxLength(256);

        builder.Property(role => role.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(role => role.Name)
            .IsUnique();
    }
}
