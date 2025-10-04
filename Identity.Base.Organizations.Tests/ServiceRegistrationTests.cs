using FluentAssertions;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Extensions;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Organizations.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddIdentityBaseOrganizations_AddsExpectedDescriptors()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new IdentityBaseModelCustomizationOptions());
        services.AddSingleton(new IdentityBaseSeedCallbacks());

        services.AddIdentityBaseOrganizations(options =>
            options.UseInMemoryDatabase("test"));

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IOrganizationService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IOrganizationMembershipService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IOrganizationRoleService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IPermissionClaimFormatter)
                                             && descriptor.ImplementationType == typeof(OrganizationClaimFormatter));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(DbContextOptions<OrganizationDbContext>));
    }
}
