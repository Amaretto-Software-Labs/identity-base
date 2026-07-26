using Identity.Base.Options;
using Identity.Base.ServicePrincipals.Domain;
using Identity.Base.ServicePrincipals.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Base.ServicePrincipals.Data;

public class ServicePrincipalDbContext(
    DbContextOptions<ServicePrincipalDbContext> options,
    IOptions<IdentityDbNamingOptions>? namingOptions = null,
    ServicePrincipalModelOptions? modelOptions = null) : DbContext(options)
{
    public DbSet<ServicePrincipal> ServicePrincipals => Set<ServicePrincipal>();
    public DbSet<ServicePrincipalCredential> ServicePrincipalCredentials => Set<ServicePrincipalCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var prefix = IdentityDbNamingOptions.Normalize(namingOptions?.Value.TablePrefix);

        modelBuilder.Entity<ServicePrincipal>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "ServicePrincipals"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.DisplayName).IsRequired().HasMaxLength(ServicePrincipal.MaxDisplayNameLength);
            entity.Property(item => item.ClientId).IsRequired().HasMaxLength(ServicePrincipal.MaxClientIdLength);
            entity.Property(item => item.ConcurrencyStamp).IsRequired().HasMaxLength(64).IsConcurrencyToken();
            entity.HasIndex(item => item.ClientId)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "ServicePrincipals_ClientId"))
                .IsUnique();
        });

        modelBuilder.Entity<ServicePrincipalCredential>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "ServicePrincipalCredentials"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Name).IsRequired().HasMaxLength(ServicePrincipalCredential.MaxNameLength);
            entity.Property(item => item.SecretHash).IsRequired().HasMaxLength(512);
            entity.Property(item => item.RevokedReason).HasMaxLength(ServicePrincipalCredential.MaxRevokedReasonLength);
            entity.HasIndex(item => new { item.ServicePrincipalId, item.Name })
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "ServicePrincipalCredentials_Principal_Name"))
                .IsUnique();
            entity.HasOne(item => item.ServicePrincipal)
                .WithMany(item => item.Credentials)
                .HasForeignKey(item => item.ServicePrincipalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        if (modelOptions is not null)
        {
            foreach (var configure in modelOptions.Customizations)
            {
                configure(modelBuilder);
            }
        }
    }
}
