using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Identity.Base.Data;
using Identity.Base.Features.Authentication.EmailManagement;
using Identity.Base.Features.Authentication.External;
using Identity.Base.Features.Authentication.Login;
using Identity.Base.Features.Authentication.Mfa;
using Identity.Base.Features.Authentication.Register;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Health;
using Identity.Base.OpenIddict;
using Identity.Base.OpenIddict.Handlers;
using Identity.Base.OpenIddict.KeyManagement;
using Identity.Base.Options;
using Identity.Base.Seeders;
using Identity.Base.Logging;
using Identity.Base.Features.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Server.AspNetCore;
using Microsoft.Net.Http.Headers;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Identity.Base.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddOpenApi();
        services.AddControllers();

        var providerFlags = ConfigureOptions(services, configuration);

        ConfigureExternalAuthenticationOptions(services, providerFlags);
        ConfigureDatabase(services);
        ConfigureIdentity(services);
        RegisterHostedServices(services);
        ConfigureCorsAndHttpClients(services);
        ConfigureOpenIddict(services, configuration, environment);
        ConfigureAuthentication(services, providerFlags);

        services.AddAuthorization();

        RegisterValidators(services);
        RegisterApplicationServices(services);
        ConfigureMfaChallengeSenders(services);
        ConfigureHealthChecks(services);

        return services;
    }

    private static ExternalProviderFlags ConfigureOptions(IServiceCollection services, IConfiguration configuration)
    {
        var externalProvidersSection = configuration.GetSection(ExternalProviderOptions.SectionName);
        var googleEnabled = externalProvidersSection.GetSection(nameof(ExternalProviderOptions.Google)).GetValue<bool?>(nameof(OAuthProviderOptions.Enabled)) ?? false;
        var microsoftEnabled = externalProvidersSection.GetSection(nameof(ExternalProviderOptions.Microsoft)).GetValue<bool?>(nameof(OAuthProviderOptions.Enabled)) ?? false;
        var appleEnabled = externalProvidersSection.GetSection(nameof(ExternalProviderOptions.Apple)).GetValue<bool?>(nameof(OAuthProviderOptions.Enabled)) ?? false;

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
            .AddOptions<MfaOptions>()
            .BindConfiguration(MfaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<ExternalProviderOptions>()
            .BindConfiguration(ExternalProviderOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<MailJetOptions>()
            .BindConfiguration(MailJetOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddOptions<OpenIddictOptions>()
            .BindConfiguration(OpenIddictOptions.SectionName)
            .ValidateDataAnnotations();

        services
            .AddOptions<OpenIddictServerKeyOptions>()
            .BindConfiguration(OpenIddictServerKeyOptions.SectionName)
            .ValidateOnStart();

        services
            .AddOptions<CorsSettings>()
            .BindConfiguration(CorsSettings.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<IValidateOptions<RegistrationOptions>, RegistrationOptionsValidator>();
        services.AddSingleton<IValidateOptions<MailJetOptions>, MailJetOptionsValidator>();
        services.AddSingleton<IValidateOptions<MfaOptions>, MfaOptionsValidator>();
        services.AddSingleton<IValidateOptions<ExternalProviderOptions>, ExternalProviderOptionsValidator>();
        services.AddSingleton<IValidateOptions<OpenIddictOptions>, OpenIddictOptionsValidator>();
        services.AddSingleton<IValidateOptions<OpenIddictServerKeyOptions>, OpenIddictServerKeyOptionsValidator>();
        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();

        return new ExternalProviderFlags(googleEnabled, microsoftEnabled, appleEnabled);
    }

    private static void ConfigureExternalAuthenticationOptions(IServiceCollection services, ExternalProviderFlags providerFlags)
    {
        if (providerFlags.GoogleEnabled)
        {
            services.AddOptions<GoogleOptions>().Configure<IOptions<ExternalProviderOptions>>((options, providerOptions) =>
            {
                var google = providerOptions.Value.Google;
                options.ClientId = google.ClientId;
                options.ClientSecret = google.ClientSecret;
                if (!string.IsNullOrWhiteSpace(google.CallbackPath))
                {
                    options.CallbackPath = google.CallbackPath;
                }

                options.Scope.Clear();
                if (google.Scopes.Count > 0)
                {
                    foreach (var scope in google.Scopes)
                    {
                        if (!string.IsNullOrWhiteSpace(scope))
                        {
                            options.Scope.Add(scope);
                        }
                    }
                }
            });
        }

        if (providerFlags.MicrosoftEnabled)
        {
            services.AddOptions<MicrosoftAccountOptions>().Configure<IOptions<ExternalProviderOptions>>((options, providerOptions) =>
            {
                var microsoft = providerOptions.Value.Microsoft;
                options.ClientId = microsoft.ClientId;
                options.ClientSecret = microsoft.ClientSecret;
                if (!string.IsNullOrWhiteSpace(microsoft.CallbackPath))
                {
                    options.CallbackPath = microsoft.CallbackPath;
                }

                options.Scope.Clear();
                if (microsoft.Scopes.Count > 0)
                {
                    foreach (var scope in microsoft.Scopes)
                    {
                        if (!string.IsNullOrWhiteSpace(scope))
                        {
                            options.Scope.Add(scope);
                        }
                    }
                }
            });
        }

        if (providerFlags.AppleEnabled)
        {
            services.AddOptions<OpenIdConnectOptions>(ExternalAuthenticationConstants.AppleScheme)
                .Configure<IOptions<ExternalProviderOptions>>((options, providerOptions) =>
                {
                    var apple = providerOptions.Value.Apple;
                    options.ClientId = apple.ClientId;
                    if (!string.IsNullOrWhiteSpace(apple.ClientSecret))
                    {
                        options.ClientSecret = apple.ClientSecret;
                    }

                    if (!string.IsNullOrWhiteSpace(apple.CallbackPath))
                    {
                        options.CallbackPath = apple.CallbackPath;
                    }

                    options.Scope.Clear();
                    if (apple.Scopes.Count > 0)
                    {
                        foreach (var scope in apple.Scopes)
                        {
                            if (!string.IsNullOrWhiteSpace(scope))
                            {
                                options.Scope.Add(scope);
                            }
                        }
                    }
                });
        }
    }

    private static void ConfigureDatabase(IServiceCollection services)
    {
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
    }

    private static void ConfigureIdentity(IServiceCollection services)
    {
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
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(3);
        });
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddHostedService<MigrationHostedService>();
        services.AddScoped<IdentityDataSeeder>();
        services.AddHostedService<IdentitySeedHostedService>();
        services.AddScoped<OpenIddictSeeder>();
        services.AddHostedService<OpenIddictSeederHostedService>();
    }

    private static void ConfigureCorsAndHttpClients(IServiceCollection services)
    {
        services.AddCors();
        services.AddSingleton<IConfigureOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>, CorsPolicyConfigurator>();
        services.AddHttpClient();
    }

    private static void ConfigureOpenIddict(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<AppDbContext>()
                    .ReplaceDefaultEntities<OpenIddictApplication, OpenIddictAuthorization, OpenIddictScope, OpenIddictToken, Guid>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetUserinfoEndpointUris("/connect/userinfo")
                       .SetIntrospectionEndpointUris("/connect/introspect");

                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow()
                       .AllowPasswordFlow();

                options.RequireProofKeyForCodeExchange();

                options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.OfflineAccess);

                options.UseConfiguredServerKeys(configuration, environment);

                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();

                options.AddEventHandler(PasswordFlowClientValidator.Descriptor);
                options.AddEventHandler(PasswordGrantHandler.Descriptor);
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });
    }

    private static void ConfigureAuthentication(IServiceCollection services, ExternalProviderFlags providerFlags)
    {
        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        authenticationBuilder.AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
            options.SlidingExpiration = false;

            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    if (!context.Response.Headers.ContainsKey(HeaderNames.WWWAuthenticate))
                    {
                        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, "error=\"login_required\"");
                    }

                    return Task.CompletedTask;
                },
                OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }
            };
        });

        authenticationBuilder.AddCookie(IdentityConstants.ExternalScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });

        authenticationBuilder.AddCookie(IdentityConstants.TwoFactorUserIdScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });

        authenticationBuilder.AddCookie(IdentityConstants.TwoFactorRememberMeScheme);

        if (providerFlags.GoogleEnabled)
        {
            authenticationBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.SaveTokens = true;
            });
        }

        if (providerFlags.MicrosoftEnabled)
        {
            authenticationBuilder.AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.SaveTokens = true;
            });
        }

        if (providerFlags.AppleEnabled)
        {
            authenticationBuilder.AddOpenIdConnect(ExternalAuthenticationConstants.AppleScheme, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.SaveTokens = true;
                options.UsePkce = true;
                options.Authority = "https://appleid.apple.com";
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.FormPost;
                options.CallbackPath = "/signin-apple";
                options.Scope.Clear();
            });
        }
    }

    private static void RegisterValidators(IServiceCollection services)
    {
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<ConfirmEmailRequest>, ConfirmEmailRequestValidator>();
        services.AddScoped<IValidator<ResendConfirmationRequest>, ResendConfirmationRequestValidator>();
        services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddScoped<IValidator<MfaVerifyRequest>, MfaVerifyRequestValidator>();
        services.AddScoped<IValidator<RegisterUserRequest>, RegisterUserRequestValidator>();
        services.AddScoped<IValidator<MfaChallengeRequest>, MfaChallengeRequestValidator>();
        services.AddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<ILogSanitizer, LogSanitizer>();
        services.AddSingleton<IExternalReturnUrlValidator, ExternalReturnUrlValidator>();
        services.AddSingleton<IExternalCallbackUriFactory, ExternalCallbackUriFactory>();
        services.AddScoped<ITemplatedEmailSender, MailJetEmailSender>();
        services.AddScoped<IAccountEmailService, AccountEmailService>();
        services.AddScoped<ExternalAuthenticationService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
    }

    private static void ConfigureMfaChallengeSenders(IServiceCollection services)
    {
        services.AddScoped<IMfaChallengeSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MfaOptions>>().Value;
            return options.Email.Enabled
                ? ActivatorUtilities.CreateInstance<EmailMfaChallengeSender>(provider)
                : ActivatorUtilities.CreateInstance<DisabledMfaChallengeSender>(
                    provider,
                    "email",
                    "Email MFA challenge is disabled.");
        });

        services.AddScoped<IMfaChallengeSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MfaOptions>>().Value;
            return options.Sms.Enabled
                ? ActivatorUtilities.CreateInstance<TwilioMfaChallengeSender>(provider)
                : ActivatorUtilities.CreateInstance<DisabledMfaChallengeSender>(
                    provider,
                    "sms",
                    "SMS MFA challenge is disabled.");
        });
    }

    private static void ConfigureHealthChecks(IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck<MailJetOptionsHealthCheck>("mailjet")
            .AddCheck<ExternalProvidersHealthCheck>("externalProviders");
    }

    private sealed record ExternalProviderFlags(bool GoogleEnabled, bool MicrosoftEnabled, bool AppleEnabled);
}
