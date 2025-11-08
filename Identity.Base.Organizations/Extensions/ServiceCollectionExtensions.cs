using System;
using FluentValidation;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.Organizations.Api.Models;
using Identity.Base.Organizations.Api.Validation;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Infrastructure;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Services;
using Identity.Base.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Identity.Base.Organizations.Extensions;

public static class ServiceCollectionExtensions
{
    public static IdentityBaseOrganizationsBuilder AddIdentityBaseOrganizations(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OrganizationOptions>();
        services.AddOptions<OrganizationRoleOptions>();
        services.AddOptions<OrganizationAuthorizationOptions>();

        services.TryAddSingleton<IdentityBaseModelCustomizationOptions>();
        services.TryAddSingleton<IdentityBaseSeedCallbacks>();

        services.TryAddScoped<IOrganizationService, OrganizationService>();
        services.TryAddScoped<IOrganizationMembershipService, OrganizationMembershipService>();
        services.TryAddScoped<IOrganizationRoleService, OrganizationRoleService>();
        services.TryAddSingleton<IOrganizationContextAccessor, OrganizationContextAccessor>();
        services.TryAddScoped<IOrganizationContext>(sp => sp.GetRequiredService<IOrganizationContextAccessor>().Current);
        services.TryAddScoped<IOrganizationScopeResolver, OrganizationScopeResolver>();
        services.TryAddScoped<OrganizationRoleSeeder>();
        services.TryAddScoped<OrganizationClaimFormatter>();
        services.TryAddScoped<IOrganizationPermissionResolver, OrganizationPermissionResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAdditionalPermissionSource, OrganizationAdditionalPermissionSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IClaimsPrincipalAugmentor, OrganizationMembershipClaimsAugmentor>());
        services.TryAddScoped<IOrganizationInvitationStore, OrganizationInvitationStore>();
        services.TryAddScoped<OrganizationInvitationService>();

        services.TryAddScoped<IValidator<CreateOrganizationRequest>, CreateOrganizationRequestValidator>();
        services.TryAddScoped<IValidator<UpdateOrganizationRequest>, UpdateOrganizationRequestValidator>();
        services.TryAddScoped<IValidator<AddMembershipRequest>, AddMembershipRequestValidator>();
        services.TryAddScoped<IValidator<UpdateMembershipRequest>, UpdateMembershipRequestValidator>();
        services.TryAddScoped<IValidator<CreateOrganizationRoleRequest>, CreateOrganizationRoleRequestValidator>();
        services.TryAddScoped<IValidator<UpdateOrganizationRolePermissionsRequest>, UpdateOrganizationRolePermissionsRequestValidator>();
        services.TryAddScoped<IValidator<CreateOrganizationInvitationRequest>, CreateOrganizationInvitationRequestValidator>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrganizationMigrationHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrganizationSeedHostedService>());

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, OrganizationPermissionAuthorizationHandler>());

        services.Replace(ServiceDescriptor.Scoped<IPermissionClaimFormatter, OrganizationClaimFormatter>());

        var builder = new IdentityBaseOrganizationsBuilder(services);
        if (configureDbContext is not null)
        {
            builder.AddDbContext<OrganizationDbContext>(configureDbContext);
        }
        else
        {
            builder.AddDbContext<OrganizationDbContext>((provider, optionsBuilder) =>
            {
                var configuration = provider.GetService<IConfiguration>();
                var connectionString = configuration?.GetConnectionString("IdentityOrganizations");
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    optionsBuilder.UseNpgsql(connectionString);
                }
            });
        }

        return builder;
    }
}
