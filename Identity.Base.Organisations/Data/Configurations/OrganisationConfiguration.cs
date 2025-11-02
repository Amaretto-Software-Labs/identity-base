using System;
using System.Collections.Generic;
using System.Text.Json;
using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organisations.Data.Configurations;

public sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Organisation> builder)
    {
        builder.ToTable("Identity_Organisations");

        builder.HasKey(organisation => organisation.Id);

        builder.Property(organisation => organisation.Slug)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(organisation => organisation.DisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(organisation => organisation.Status)
            .HasConversion<int>()
            .HasDefaultValue(OrganisationStatus.Active);

        builder.Property(organisation => organisation.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(organisation => organisation.UpdatedAtUtc);
        builder.Property(organisation => organisation.ArchivedAtUtc);

        builder.Property(organisation => organisation.Metadata)
            .HasConversion(
                metadata => SerializeMetadata(metadata),
                json => DeserializeMetadata(json))
            .HasColumnName("Metadata")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .Metadata.SetValueComparer(new ValueComparer<OrganisationMetadata>(
                (left, right) => SerializeMetadata(left) == SerializeMetadata(right),
                metadata => SerializeMetadata(metadata).GetHashCode(),
                metadata => CloneMetadata(metadata)));

        builder.HasIndex(organisation => new { organisation.TenantId, organisation.Slug })
            .IsUnique()
            .HasDatabaseName("IX_Organisations_Tenant_Slug");

        builder.HasIndex(organisation => new { organisation.TenantId, organisation.DisplayName })
            .IsUnique()
            .HasDatabaseName("IX_Organisations_Tenant_DisplayName");
    }

    private static string SerializeMetadata(OrganisationMetadata? metadata)
    {
        return JsonSerializer.Serialize(metadata?.Values ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions);
    }

    private static OrganisationMetadata DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return OrganisationMetadata.Empty;
        }

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions)
                         ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        return new OrganisationMetadata(dictionary);
    }

    private static OrganisationMetadata CloneMetadata(OrganisationMetadata? metadata)
        => metadata is null ? OrganisationMetadata.Empty : new OrganisationMetadata(metadata.Values);
}
