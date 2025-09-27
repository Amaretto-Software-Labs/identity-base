using Identity.Base.Identity;
using Identity.Base.OpenIddict;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("Identity_UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("Identity_UserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("Identity_UserTokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("Identity_RoleClaims");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("Identity_UserRoles");

        modelBuilder.Entity<OpenIddictApplication>().ToTable("Identity_OpenIddictApplications");
        modelBuilder.Entity<OpenIddictAuthorization>().ToTable("Identity_OpenIddictAuthorizations");
        modelBuilder.Entity<OpenIddictScope>().ToTable("Identity_OpenIddictScopes");
        modelBuilder.Entity<OpenIddictToken>().ToTable("Identity_OpenIddictTokens");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
