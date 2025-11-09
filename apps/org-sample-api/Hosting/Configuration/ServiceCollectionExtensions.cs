using System;
using System.Collections.Generic;
using FluentValidation;
using Identity.Base.Data;
using Identity.Base.Features.Authentication.EmailManagement;
using Identity.Base.Identity;
using Identity.Base.Options;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Extensions;
using Identity.Base.Admin.Configuration;
using Identity.Base.Abstractions;
using Identity.Base.Extensions;
using Identity.Base.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using OrgSampleApi.Sample;
using OrgSampleApi.Sample.Invitations;
using OrgSampleApi.Sample.Members;
using OrgSampleApi.Hosting.Infrastructure;
using Serilog;

namespace OrgSampleApi.Hosting.Configuration;

internal static class ServiceCollectionExtensions
{
    public static void AddOrgSampleLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "OrgSampleApi")
                .WriteTo.Console();
        });
    }

    public static void AddOrgSampleOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OrganizationBootstrapOptions>(configuration.GetSection("SampleData:DefaultOrganization"));
        services.Configure<InvitationLinkOptions>(configuration.GetSection("Invitations"));
    }

    public static void AddOrgSampleCoreServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<OrganizationBootstrapService>();
        services.AddScoped<IUserCreationListener, OrganizationBootstrapUserCreationListener>();
        services.AddScoped<IValidator<InvitationRegistrationRequest>, InvitationRegistrationRequestValidator>();
        services.AddScoped<OrganizationMemberDirectory>();
    }

    public static void ConfigureIdentityBase(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var migrationsAssembly = typeof(ServiceCollectionExtensions).Assembly.FullName;

        var configureAppDbContext = new Action<IServiceProvider, DbContextOptionsBuilder>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            if (options is DbContextOptionsBuilder<AppDbContext> typed)
            {
                typed.UseOrgSampleProvider(config, migrationsAssembly);
            }
            else
            {
                throw new InvalidOperationException("Unable to configure AppDbContext options.");
            }
        });

        var configureRolesDbContext = new Action<IServiceProvider, DbContextOptionsBuilder>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            if (options is DbContextOptionsBuilder<IdentityRolesDbContext> typed)
            {
                typed.UseOrgSampleProvider(config, migrationsAssembly);
            }
            else
            {
                throw new InvalidOperationException("Unable to configure IdentityRolesDbContext options.");
            }
        });

        var configureOrganizationDbContext = new Action<IServiceProvider, DbContextOptionsBuilder>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            if (options is DbContextOptionsBuilder<OrganizationDbContext> typed)
            {
                typed.UseOrgSampleProvider(config, migrationsAssembly);
            }
            else
            {
                throw new InvalidOperationException("Unable to configure OrganizationDbContext options.");
            }
        });

        const string TablePrefix = "OrgSample";

        var identityBuilder = services.AddIdentityBase(configuration, environment, configureDbContext: configureAppDbContext);
        identityBuilder
            .UseTablePrefix(TablePrefix)
            .AddConfiguredExternalProviders();

        services.AddIdentityAdmin(configuration, configureRolesDbContext)
            .UseTablePrefix(TablePrefix);
        services.AddIdentityBaseOrganizations(configureOrganizationDbContext)
            .UseTablePrefix(TablePrefix);

        // Organization options (Organizations, RoleOptions, Authorization) are bound by default in the package.

        identityBuilder.AfterOrganizationSeed(async (serviceProvider, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            var seedOptions = scopedServices.GetRequiredService<IOptions<IdentitySeedOptions>>().Value;
            var defaults = scopedServices.GetRequiredService<IOptions<OrganizationBootstrapOptions>>().Value;

            if (!seedOptions.Enabled || string.IsNullOrWhiteSpace(seedOptions.Email) ||
                string.IsNullOrWhiteSpace(defaults.Slug) || string.IsNullOrWhiteSpace(defaults.DisplayName))
            {
                return;
            }

            var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(seedOptions.Email).ConfigureAwait(false);
            if (user is null)
            {
                return;
            }

            var bootstrapService = scopedServices.GetRequiredService<OrganizationBootstrapService>();
            var metadata = defaults.Metadata ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var name = string.IsNullOrWhiteSpace(defaults.DisplayName) ? defaults.Slug : defaults.DisplayName;
            var request = new OrganizationBootstrapRequest(name, defaults.Slug, metadata);

            await bootstrapService.EnsureOrganizationOwnerAsync(user, request, cancellationToken).ConfigureAwait(false);
        });
    }

}
