using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Roles.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("Identity_AuditEntries");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Action)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(audit => audit.Metadata)
            .HasColumnType("jsonb");

        builder.HasIndex(audit => audit.ActorUserId);
        builder.HasIndex(audit => audit.TargetUserId);
        builder.HasIndex(audit => audit.CreatedAt);
    }
}
