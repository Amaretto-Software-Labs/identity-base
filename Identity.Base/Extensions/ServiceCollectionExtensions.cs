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

        services.AddSingleton<IValidateOptions<RegistrationOptions>, RegistrationOptionsValidator>();
        services.AddSingleton<IValidateOptions<MailJetOptions>, MailJetOptionsValidator>();
        services.AddSingleton<IValidateOptions<MfaOptions>, MfaOptionsValidator>();
        services.AddSingleton<IValidateOptions<ExternalProviderOptions>, ExternalProviderOptionsValidator>();
        services
            .AddOptions<OpenIddictOptions>()
            .BindConfiguration(OpenIddictOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<IValidateOptions<OpenIddictOptions>, OpenIddictOptionsValidator>();
        services
            .AddOptions<CorsSettings>()
            .BindConfiguration(CorsSettings.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();

        if (googleEnabled)
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

        if (microsoftEnabled)
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

        if (appleEnabled)
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
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(3);
        });

        services.AddHostedService<MigrationHostedService>();
        services.AddScoped<IdentityDataSeeder>();
        services.AddHostedService<IdentitySeedHostedService>();
        services.AddScoped<OpenIddictSeeder>();
        services.AddHostedService<OpenIddictSeederHostedService>();

        services.AddCors();
        services.AddSingleton<IConfigureOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>, CorsPolicyConfigurator>();
        services.AddHttpClient();

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

                options.AddEphemeralEncryptionKey(); // Use ephemeral key but don't encrypt tokens
                options.AddDevelopmentSigningCertificate();

                // Disable token encryption for JWT Bearer compatibility
                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();

                options.AddEventHandler(PasswordGrantHandler.Descriptor);
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

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
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10); // Give more time for MFA completion
        });

        authenticationBuilder.AddCookie(IdentityConstants.TwoFactorRememberMeScheme);

        if (googleEnabled)
        {
            authenticationBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.SaveTokens = true;
            });
        }

        if (microsoftEnabled)
        {
            authenticationBuilder.AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.SaveTokens = true;
            });
        }

        if (appleEnabled)
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

        services.AddAuthorization();

        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<ConfirmEmailRequest>, ConfirmEmailRequestValidator>();
        services.AddScoped<IValidator<ResendConfirmationRequest>, ResendConfirmationRequestValidator>();
        services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddScoped<IValidator<MfaVerifyRequest>, MfaVerifyRequestValidator>();
        services.AddScoped<IValidator<RegisterUserRequest>, RegisterUserRequestValidator>();
        services.AddScoped<IValidator<MfaChallengeRequest>, MfaChallengeRequestValidator>();
        services.AddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();

        services.AddScoped<ITemplatedEmailSender, MailJetEmailSender>();
        services.AddScoped<IAccountEmailService, AccountEmailService>();
        services.AddScoped<ExternalAuthenticationService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
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

        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck<MailJetOptionsHealthCheck>("mailjet")
            .AddCheck<ExternalProvidersHealthCheck>("externalProviders");

        return services;
    }
}
