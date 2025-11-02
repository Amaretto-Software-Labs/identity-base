using System;
using System.Text.Json;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data.Entities;
using Identity.Base.Organisations.Data.ValueComparers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organisations.Data.Configurations;

internal sealed class OrganisationInvitationConfiguration : IEntityTypeConfiguration<OrganisationInvitationEntity>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<OrganisationInvitationEntity> builder)
    {
        builder.ToTable("Identity_OrganisationInvitations");

        builder.HasKey(entity => entity.Code).HasName("PK_Identity_OrganisationInvitations");
        builder.Property(entity => entity.OrganisationId).IsRequired();
        builder.Property(entity => entity.OrganisationSlug).IsRequired().HasMaxLength(128);
        builder.Property(entity => entity.OrganisationName).IsRequired().HasMaxLength(256);
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
            .Metadata.SetValueComparer(OrganisationInvitationValueComparers.RoleIds);

        builder.HasIndex(entity => entity.OrganisationId)
            .HasDatabaseName("IX_Identity_OrganisationInvitations_OrganisationId");
        builder.HasIndex(entity => entity.Email)
            .HasDatabaseName("IX_Identity_OrganisationInvitations_Email");
        builder.HasIndex(entity => entity.UsedAtUtc)
            .HasDatabaseName("IX_Identity_OrganisationInvitations_UsedAtUtc");
    }
}
