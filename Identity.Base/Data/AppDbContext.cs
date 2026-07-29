using Identity.Base.Identity;
using Identity.Base.Features.Authentication.Passkeys;
using Identity.Base.OpenIddict;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>,
        ApplicationUserPasskey>(options)
{
    public DbSet<OpenIddictApplication> OpenIddictApplications => Set<OpenIddictApplication>();
    public DbSet<OpenIddictAuthorization> OpenIddictAuthorizations => Set<OpenIddictAuthorization>();
    public DbSet<OpenIddictScope> OpenIddictScopes => Set<OpenIddictScope>();
    public DbSet<OpenIddictToken> OpenIddictTokens => Set<OpenIddictToken>();
    internal DbSet<PasskeyRegistrationDraft> PasskeyRegistrationDrafts => Set<PasskeyRegistrationDraft>();
    internal DbSet<PasskeyRecoveryDraft> PasskeyRecoveryDrafts => Set<PasskeyRecoveryDraft>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ConfigureTableNaming(modelBuilder);

        var customizationOptions = this.GetService<IDbContextOptions>()
                ?.FindExtension<IdentityBaseModelCustomizationOptionsExtension>()
                ?.Options
            ?? ((IInfrastructure<IServiceProvider>)this).Instance?.GetService<IdentityBaseModelCustomizationOptions>();
        if (customizationOptions is not null)
        {
            foreach (var configure in customizationOptions.AppDbContextCustomizations)
            {
                configure(modelBuilder);
            }
        }
    }

    private void ConfigureTableNaming(ModelBuilder modelBuilder)
    {
        var prefix = IdentityDbNamingHelper.ResolveTablePrefix(this);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "Users"));
            entity.HasIndex(user => user.Email)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Users_Email"));
            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Users_NormalizedEmail"))
                .IsUnique();
            entity.HasIndex(user => user.NormalizedUserName)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Users_NormalizedUserName"))
                .IsUnique();
            entity.HasIndex(user => user.CreatedAt)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Users_CreatedAt"));
        });

        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "Roles"));
            entity.HasIndex(role => role.NormalizedName)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Roles_NormalizedName"))
                .IsUnique();
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "UserClaims"));
        modelBuilder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "UserLogins"));
            // Preserve the pre-.NET 10 schema widths when enabling Identity schema v3.
            var keyMaxLength = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
                ? (int?)null
                : 450;
            entity.Property(login => login.LoginProvider).Metadata.SetMaxLength(keyMaxLength);
            entity.Property(login => login.ProviderKey).Metadata.SetMaxLength(keyMaxLength);
        });
        modelBuilder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "UserTokens"));
            var keyMaxLength = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
                ? (int?)null
                : 450;
            entity.Property(token => token.LoginProvider).Metadata.SetMaxLength(keyMaxLength);
            entity.Property(token => token.Name).Metadata.SetMaxLength(keyMaxLength);
        });
        modelBuilder.Entity<ApplicationUser>()
            .Property(user => user.PhoneNumber)
            .Metadata.SetMaxLength(null);
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "RoleClaims"));
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "UserRoles"));

        if (modelBuilder.Model.FindEntityType(typeof(ApplicationUserPasskey)) is not null)
        {
            modelBuilder.Entity<ApplicationUserPasskey>(entity =>
            {
                entity.ToTable(IdentityDbNamingHelper.Table(prefix, "UserPasskeys"));
                entity.HasKey(passkey => passkey.Id);
                entity.HasIndex(passkey => passkey.CredentialId)
                    .IsUnique()
                    .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "UserPasskeys_CredentialId"));
                entity.HasIndex(passkey => passkey.UserId)
                    .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "UserPasskeys_UserId"));
                entity.Property(passkey => passkey.ConcurrencyStamp)
                    .HasMaxLength(32)
                    .IsRequired()
                    .IsConcurrencyToken();
            });

            modelBuilder.Entity<PasskeyRegistrationDraft>(entity =>
            {
                entity.ToTable(IdentityDbNamingHelper.Table(prefix, "PasskeyRegistrationDrafts"));
                entity.HasKey(draft => draft.Id);
                entity.Property(draft => draft.Email).HasMaxLength(256).IsRequired();
                entity.Property(draft => draft.NormalizedEmail).HasMaxLength(256).IsRequired();
                entity.Property(draft => draft.Mode).HasMaxLength(32).IsRequired();
                entity.Property(draft => draft.ClientId).HasMaxLength(100).IsRequired();
                entity.Property(draft => draft.ProfileMetadataJson).IsRequired();
                entity.Property(draft => draft.DisplayName).HasMaxLength(ApplicationUser.DisplayNameMaxLength);
                entity.Property(draft => draft.ConfirmationTokenHash).HasMaxLength(32).IsRequired();
                entity.Property(draft => draft.ConcurrencyStamp)
                    .HasMaxLength(32)
                    .IsRequired()
                    .IsConcurrencyToken();
                entity.HasIndex(draft => draft.NormalizedEmail)
                    .IsUnique()
                    .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "PasskeyRegistrationDrafts_NormalizedEmail"));
                entity.HasIndex(draft => draft.ExpiresAt)
                    .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "PasskeyRegistrationDrafts_ExpiresAt"));
            });

            modelBuilder.Entity<PasskeyRecoveryDraft>(entity =>
            {
                entity.ToTable(IdentityDbNamingHelper.Table(prefix, "PasskeyRecoveryDrafts"));
                entity.HasKey(draft => draft.Id);
                entity.Property(draft => draft.ClientId).HasMaxLength(100).IsRequired();
                entity.Property(draft => draft.ConfirmationTokenHash).HasMaxLength(32).IsRequired();
                entity.Property(draft => draft.ConcurrencyStamp)
                    .HasMaxLength(32)
                    .IsRequired()
                    .IsConcurrencyToken();
                entity.HasIndex(draft => draft.UserId)
                    .IsUnique()
                    .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "PasskeyRecoveryDrafts_UserId"));
                entity.HasIndex(draft => draft.ExpiresAt)
                    .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "PasskeyRecoveryDrafts_ExpiresAt"));
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(draft => draft.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
        else
        {
            modelBuilder.Ignore<PasskeyRegistrationDraft>();
            modelBuilder.Ignore<PasskeyRecoveryDraft>();
        }

        modelBuilder.Entity<OpenIddictApplication>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictApplications"));
        modelBuilder.Entity<OpenIddictAuthorization>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictAuthorizations"));
        modelBuilder.Entity<OpenIddictScope>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictScopes"));
        modelBuilder.Entity<OpenIddictToken>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictTokens"));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RotatePasskeyConcurrencyStamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RotatePasskeyConcurrencyStamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RotatePasskeyConcurrencyStamps()
    {
        foreach (var entry in ChangeTracker.Entries<ApplicationUserPasskey>()
                     .Where(entry => entry.State == EntityState.Modified))
        {
            entry.Entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }
    }
}
