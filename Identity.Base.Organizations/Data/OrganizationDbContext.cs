using Identity.Base.Options;
using Identity.Base.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Organizations.Data;

public class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();

    public DbSet<OrganizationRoleAssignment> OrganizationRoleAssignments => Set<OrganizationRoleAssignment>();

    public DbSet<OrganizationRolePermission> OrganizationRolePermissions => Set<OrganizationRolePermission>();

    public DbSet<Entities.OrganizationInvitationEntity> OrganizationInvitations => Set<Entities.OrganizationInvitationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);

        var customizationOptions = this.GetService<IDbContextOptions>()
                ?.FindExtension<IdentityBaseModelCustomizationOptionsExtension>()
                ?.Options
            ?? ((IInfrastructure<IServiceProvider>)this).Instance?.GetService<IdentityBaseModelCustomizationOptions>();
        if (customizationOptions is null)
        {
            return;
        }

        foreach (var configure in customizationOptions.OrganizationDbContextCustomizations)
        {
            configure(modelBuilder);
        }
    }
}
