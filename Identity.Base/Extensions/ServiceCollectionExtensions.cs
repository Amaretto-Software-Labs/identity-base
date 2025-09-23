using FluentValidation;
using Identity.Base.Data;
using Identity.Base.Features.Authentication.Register;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Base.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();

        services
            .AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Primary),
                "ConnectionStrings:Primary must be provided.")
            .ValidateOnStart();

        services
            .AddOptions<IdentitySeedOptions>()
            .BindConfiguration(IdentitySeedOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<RegistrationOptions>()
            .BindConfiguration(RegistrationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<MailJetOptions>()
            .BindConfiguration(MailJetOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<IValidateOptions<RegistrationOptions>, RegistrationOptionsValidator>();
        services.AddSingleton<IValidateOptions<MailJetOptions>, MailJetOptionsValidator>();

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var connectionString = databaseOptions.Primary ?? string.Empty;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:Primary must be provided.");
            }

            if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
            {
                var databaseName = connectionString.Substring("InMemory:".Length);
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    databaseName = "IdentityBaseTests";
                }
                options.UseInMemoryDatabase(databaseName);
            }
            else
            {
                options.UseNpgsql(
                    connectionString,
                    builder => builder.EnableRetryOnFailure());
            }
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 12;

                options.SignIn.RequireConfirmedEmail = true;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(3);
        });

        services.AddAuthentication();
        services.AddAuthorization();

        services.AddScoped<IdentityDataSeeder>();
        services.AddHostedService<IdentitySeedHostedService>();

        services.AddScoped<IValidator<RegisterUserRequest>, RegisterUserRequestValidator>();

        services.AddScoped<ITemplatedEmailSender, MailJetEmailSender>();

        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}
