using Identity.Base.Identity;
using Identity.Base.OpenIddict;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<OpenIddictApplication> OpenIddictApplications => Set<OpenIddictApplication>();
    public DbSet<OpenIddictAuthorization> OpenIddictAuthorizations => Set<OpenIddictAuthorization>();
    public DbSet<OpenIddictScope> OpenIddictScopes => Set<OpenIddictScope>();
    public DbSet<OpenIddictToken> OpenIddictTokens => Set<OpenIddictToken>();
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
                .IsUnique()
                .HasFilter("\"NormalizedEmail\" IS NOT NULL");
            entity.HasIndex(user => user.NormalizedUserName)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Users_NormalizedUserName"))
                .IsUnique()
                .HasFilter("\"NormalizedUserName\" IS NOT NULL");
            entity.HasIndex(user => user.CreatedAt)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Users_CreatedAt"));
        });

        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable(IdentityDbNamingHelper.Table(prefix, "Roles"));
            entity.HasIndex(role => role.NormalizedName)
                .HasDatabaseName(IdentityDbNamingHelper.Index(prefix, "Roles_NormalizedName"))
                .IsUnique()
                .HasFilter("\"NormalizedName\" IS NOT NULL");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "UserClaims"));
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "UserLogins"));
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "UserTokens"));
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "RoleClaims"));
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable(IdentityDbNamingHelper.Table(prefix, "UserRoles"));

        modelBuilder.Entity<OpenIddictApplication>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictApplications"));
        modelBuilder.Entity<OpenIddictAuthorization>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictAuthorizations"));
        modelBuilder.Entity<OpenIddictScope>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictScopes"));
        modelBuilder.Entity<OpenIddictToken>().ToTable(IdentityDbNamingHelper.Table(prefix, "OpenIddictTokens"));
    }
}
