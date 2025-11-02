using Identity.Base.Options;
using Identity.Base.Organisations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Organisations.Data;

public class OrganisationDbContext(DbContextOptions<OrganisationDbContext> options)
    : DbContext(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();

    public DbSet<OrganisationMembership> OrganisationMemberships => Set<OrganisationMembership>();

    public DbSet<OrganisationRole> OrganisationRoles => Set<OrganisationRole>();

    public DbSet<OrganisationRoleAssignment> OrganisationRoleAssignments => Set<OrganisationRoleAssignment>();

    public DbSet<OrganisationRolePermission> OrganisationRolePermissions => Set<OrganisationRolePermission>();

    public DbSet<Entities.OrganisationInvitationEntity> OrganisationInvitations => Set<Entities.OrganisationInvitationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganisationDbContext).Assembly);

        var customizationOptions = this.GetService<IDbContextOptions>()
                ?.FindExtension<IdentityBaseModelCustomizationOptionsExtension>()
                ?.Options
            ?? ((IInfrastructure<IServiceProvider>)this).Instance?.GetService<IdentityBaseModelCustomizationOptions>();
        if (customizationOptions is null)
        {
            return;
        }

        foreach (var configure in customizationOptions.OrganisationDbContextCustomizations)
        {
            configure(modelBuilder);
        }
    }
}
