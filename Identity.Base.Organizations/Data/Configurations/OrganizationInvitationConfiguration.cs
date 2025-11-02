using System;
using System.Text.Json;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data.Entities;
using Identity.Base.Organizations.Data.ValueComparers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

internal sealed class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitationEntity>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<OrganizationInvitationEntity> builder)
    {
        builder.ToTable("Identity_OrganizationInvitations");

        builder.HasKey(entity => entity.Code).HasName("PK_Identity_OrganizationInvitations");
        builder.Property(entity => entity.OrganizationId).IsRequired();
        builder.Property(entity => entity.OrganizationSlug).IsRequired().HasMaxLength(128);
        builder.Property(entity => entity.OrganizationName).IsRequired().HasMaxLength(256);
        builder.Property(entity => entity.Email).IsRequired().HasMaxLength(256);
        builder.Property(entity => entity.CreatedAtUtc).IsRequired();
        builder.Property(entity => entity.ExpiresAtUtc).IsRequired();
        builder.Property(entity => entity.UsedAtUtc);
        builder.Property(entity => entity.UsedByUserId);
        builder.Property(entity => entity.CreatedBy);

        builder.Property(entity => entity.RoleIds)
            .HasColumnType("jsonb")
            .HasConversion(
                roleIds => JsonSerializer.Serialize(roleIds ?? Array.Empty<Guid>(), SerializerOptions),
                json => string.IsNullOrWhiteSpace(json)
                    ? Array.Empty<Guid>()
                    : JsonSerializer.Deserialize<Guid[]>(json, SerializerOptions) ?? Array.Empty<Guid>())
            .Metadata.SetValueComparer(OrganizationInvitationValueComparers.RoleIds);

        builder.HasIndex(entity => entity.OrganizationId)
            .HasDatabaseName("IX_Identity_OrganizationInvitations_OrganizationId");
        builder.HasIndex(entity => entity.Email)
            .HasDatabaseName("IX_Identity_OrganizationInvitations_Email");
        builder.HasIndex(entity => entity.UsedAtUtc)
            .HasDatabaseName("IX_Identity_OrganizationInvitations_UsedAtUtc");
    }
}
