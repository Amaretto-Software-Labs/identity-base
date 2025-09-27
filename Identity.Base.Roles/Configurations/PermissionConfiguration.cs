using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Roles.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Identity_Permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(permission => permission.Description)
            .HasMaxLength(256);

        builder.HasIndex(permission => permission.Name)
            .IsUnique();
    }
}
