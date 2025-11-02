using Shouldly;
using Identity.Base.Identity;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Extensions;
using Identity.Base.Organisations.Services;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Organisations.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddIdentityBaseOrganisations_AddsExpectedDescriptors()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new IdentityBaseModelCustomizationOptions());
        services.AddSingleton(new IdentityBaseSeedCallbacks());

        services.AddIdentityBaseOrganisations(options =>
            options.UseInMemoryDatabase("test"));

        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IOrganisationService));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IOrganisationMembershipService));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IOrganisationRoleService));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IPermissionClaimFormatter)
                                             && descriptor.ImplementationType == typeof(OrganisationClaimFormatter));
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(DbContextOptions<OrganisationDbContext>));
    }
}
