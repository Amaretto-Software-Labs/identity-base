using System;
using System.Collections.Generic;
using System.Text.Json;
using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Organizations.Data.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Slug)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(organization => organization.DisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(organization => organization.Status)
            .HasConversion<int>()
            .HasDefaultValue(OrganizationStatus.Active);

        builder.Property(organization => organization.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(organization => organization.UpdatedAtUtc);
        builder.Property(organization => organization.ArchivedAtUtc);

        builder.Property(organization => organization.Metadata)
            .HasConversion(
                metadata => SerializeMetadata(metadata),
                json => DeserializeMetadata(json))
            .HasColumnName("Metadata")
            .Metadata.SetValueComparer(new ValueComparer<OrganizationMetadata>(
                (left, right) => SerializeMetadata(left) == SerializeMetadata(right),
                metadata => SerializeMetadata(metadata).GetHashCode(),
                metadata => CloneMetadata(metadata)));

        builder.HasIndex(organization => new { organization.TenantId, organization.Slug })
            .IsUnique();

        builder.HasIndex(organization => new { organization.TenantId, organization.DisplayName })
            .IsUnique();
    }

    private static string SerializeMetadata(OrganizationMetadata? metadata)
    {
        return JsonSerializer.Serialize(metadata?.Values ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), JsonOptions);
    }

    private static OrganizationMetadata DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return OrganizationMetadata.Empty;
        }

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions)
                         ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        return new OrganizationMetadata(dictionary);
    }

    private static OrganizationMetadata CloneMetadata(OrganizationMetadata? metadata)
        => metadata is null ? OrganizationMetadata.Empty : new OrganizationMetadata(metadata.Values);
}
