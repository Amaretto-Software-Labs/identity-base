using System;
using FluentValidation;
using Identity.Base.Identity;
using Identity.Base.Organisations.Api.Models;
using Identity.Base.Organisations.Api.Validation;
using Identity.Base.Organisations.Authorization;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Infrastructure;
using Identity.Base.Organisations.Options;
using Identity.Base.Organisations.Services;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Services;
using Identity.Base.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Identity.Base.Organisations.Extensions;

public static class ServiceCollectionExtensions
{
    public static IdentityBaseOrganisationsBuilder AddIdentityBaseOrganisations(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OrganisationOptions>();
        services.AddOptions<OrganisationRoleOptions>();

        services.TryAddSingleton<IdentityBaseModelCustomizationOptions>();
        services.TryAddSingleton<IdentityBaseSeedCallbacks>();

        services.TryAddScoped<IOrganisationService, OrganisationService>();
        services.TryAddScoped<IOrganisationMembershipService, OrganisationMembershipService>();
        services.TryAddScoped<IOrganisationRoleService, OrganisationRoleService>();
        services.TryAddSingleton<IOrganisationContextAccessor, OrganisationContextAccessor>();
        services.TryAddScoped<IOrganisationContext>(sp => sp.GetRequiredService<IOrganisationContextAccessor>().Current);
        services.TryAddScoped<IOrganisationScopeResolver, OrganisationScopeResolver>();
        services.TryAddScoped<OrganisationRoleSeeder>();
        services.TryAddScoped<OrganisationClaimFormatter>();
        services.TryAddScoped<IOrganisationPermissionResolver, OrganisationPermissionResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAdditionalPermissionSource, OrganisationAdditionalPermissionSource>());
        services.TryAddScoped<IOrganisationInvitationStore, OrganisationInvitationStore>();
        services.TryAddScoped<OrganisationInvitationService>();

        services.TryAddScoped<IValidator<CreateOrganisationRequest>, CreateOrganisationRequestValidator>();
        services.TryAddScoped<IValidator<UpdateOrganisationRequest>, UpdateOrganisationRequestValidator>();
        services.TryAddScoped<IValidator<AddMembershipRequest>, AddMembershipRequestValidator>();
        services.TryAddScoped<IValidator<UpdateMembershipRequest>, UpdateMembershipRequestValidator>();
        services.TryAddScoped<IValidator<CreateOrganisationRoleRequest>, CreateOrganisationRoleRequestValidator>();
        services.TryAddScoped<IValidator<UpdateOrganisationRolePermissionsRequest>, UpdateOrganisationRolePermissionsRequestValidator>();
        services.TryAddScoped<IValidator<SetActiveOrganisationRequest>, SetActiveOrganisationRequestValidator>();
        services.TryAddScoped<IValidator<CreateOrganisationInvitationRequest>, CreateOrganisationInvitationRequestValidator>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrganisationMigrationHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrganisationSeedHostedService>());

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, OrganisationPermissionAuthorizationHandler>());

        services.Replace(ServiceDescriptor.Scoped<IPermissionClaimFormatter, OrganisationClaimFormatter>());

        var builder = new IdentityBaseOrganisationsBuilder(services);
        if (configureDbContext is not null)
        {
            builder.AddDbContext<OrganisationDbContext>(configureDbContext);
        }
        else
        {
            builder.AddDbContext<OrganisationDbContext>((provider, optionsBuilder) =>
            {
                var configuration = provider.GetService<IConfiguration>();
                var connectionString = configuration?.GetConnectionString("IdentityOrganisations");
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    optionsBuilder.UseNpgsql(connectionString);
                }
            });
        }

        return builder;
    }
}
