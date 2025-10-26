using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OrgSampleApi.Sample.Data;

public sealed class OrgSampleDbContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public OrgSampleDbContext(DbContextOptions<OrgSampleDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrganizationInvitation> Invitations => Set<OrganizationInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var invitation = modelBuilder.Entity<OrganizationInvitation>();
        invitation.ToTable("Identity_OrganizationInvitations");

        invitation.HasKey(entity => entity.Code).HasName("PK_Identity_OrganizationInvitations");
        invitation.Property(entity => entity.OrganizationId).IsRequired();
        invitation.Property(entity => entity.OrganizationSlug).IsRequired().HasMaxLength(128);
        invitation.Property(entity => entity.OrganizationName).IsRequired().HasMaxLength(256);
        invitation.Property(entity => entity.Email).IsRequired().HasMaxLength(256);
        invitation.Property(entity => entity.CreatedAtUtc).IsRequired();
        invitation.Property(entity => entity.ExpiresAtUtc).IsRequired();

        invitation.Property(entity => entity.RoleIds)
            .HasColumnType("jsonb")
            .HasConversion(
                roleIds => JsonSerializer.Serialize(roleIds ?? Array.Empty<Guid>(), SerializerOptions),
                json => string.IsNullOrWhiteSpace(json)
                    ? Array.Empty<Guid>()
                    : JsonSerializer.Deserialize<Guid[]>(json, SerializerOptions) ?? Array.Empty<Guid>())
            .Metadata.SetValueComparer(OrgSampleValueComparers.RoleIds);

        invitation.HasIndex(entity => entity.OrganizationId)
            .HasDatabaseName("IX_Identity_OrganizationInvitations_OrganizationId");
        invitation.HasIndex(entity => entity.Email)
            .HasDatabaseName("IX_Identity_OrganizationInvitations_Email");
    }
}
