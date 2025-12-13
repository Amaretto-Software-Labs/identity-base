using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Identity.Base.Abstractions;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Data;
using Identity.Base.Features.Authentication.EmailManagement;
using Identity.Base.Features.Authentication.External;
using Identity.Base.Features.Authentication.Login;
using Identity.Base.Features.Authentication.Mfa;
using Identity.Base.Features.Authentication.Register;
using Identity.Base.Features.Email;
using Identity.Base.Features.Notifications;
using Identity.Base.Features.Users;
using Identity.Base.Health;
using Identity.Base.Identity;
using Identity.Base.Lifecycle;
using Identity.Base.Logging;
using Identity.Base.OpenIddict;
using Identity.Base.OpenIddict.Handlers;
using Identity.Base.OpenIddict.KeyManagement;
using Identity.Base.Options;
using Identity.Base.Seeders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace Identity.Base.Extensions;

public sealed class IdentityBaseBuilder
{
    private readonly IdentityBaseOptions _options;
    private readonly Action<IServiceProvider, DbContextOptionsBuilder>? _configureAppDbContext;
    private readonly IdentityBaseModelCustomizationOptions _modelCustomizationOptions = new();
    private readonly IdentityBaseSeedCallbacks _seedCallbacks = new();

    internal IdentityBaseBuilder(
        IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IdentityBaseOptions options,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureAppDbContext)
    {
        Services = services;
        Configuration = configuration;
        Environment = environment;
        _options = options;
        _configureAppDbContext = configureAppDbContext;
    }

    public IServiceCollection Services { get; }

    internal IdentityBaseModelCustomizationOptions ModelCustomizationOptions => _modelCustomizationOptions;

    internal IdentityBaseSeedCallbacks SeedCallbacks => _seedCallbacks;

    public IConfiguration Configuration { get; }

    internal IWebHostEnvironment Environment { get; }

    internal AuthenticationBuilder AuthenticationBuilder { get; private set; } = null!;

    internal ExternalProviderFlags ProviderFlags { get; private set; }
        = ExternalProviderFlags.None;

    internal IdentityBaseBuilder Initialize()
    {
        Services.AddOpenApi();
        Services.AddOptions<IdentityDbNamingOptions>();
        Services.AddControllers();
        Services.TryAddSingleton(_ => _modelCustomizationOptions);
        Services.TryAddSingleton(_ => _seedCallbacks);
        RegisterTenantContextAccessor();

        ProviderFlags = ConfigureOptions();
        ConfigureDatabase();
        ConfigureIdentity();
        RegisterHostedServices();
        ConfigureCorsAndHttpClients();
        ConfigureOpenIddict();
        ConfigureAuthentication();
        Services.AddAuthorization();

        RegisterValidators();
        RegisterApplicationServices();
        ConfigureMfaChallengeSenders();
        ConfigureHealthChecks();

        return this;
    }

    public IdentityBaseBuilder ConfigureAppDbContextModel(Action<ModelBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _modelCustomizationOptions.AddAppDbContextCustomization(configure);
        return this;
    }

    public IdentityBaseBuilder UseTablePrefix(string tablePrefix)
    {
        var normalized = IdentityDbNamingOptions.Normalize(tablePrefix);
        Services.Configure<IdentityDbNamingOptions>(options => options.TablePrefix = normalized);
        return this;
    }

    public IdentityBaseBuilder ConfigureIdentityRolesModel(Action<ModelBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _modelCustomizationOptions.AddIdentityRolesDbContextCustomization(configure);
        return this;
    }

    public IdentityBaseBuilder UseTemplatedEmailSender<TSender>() where TSender : class, ITemplatedEmailSender
    {
        Services.Replace(ServiceDescriptor.Scoped<ITemplatedEmailSender, TSender>());
        return this;
    }

    public IdentityBaseBuilder UseTemplatedEmailSender(Func<IServiceProvider, ITemplatedEmailSender> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Services.Replace(ServiceDescriptor.Scoped(typeof(ITemplatedEmailSender), factory));
        return this;
    }

    public IdentityBaseBuilder AfterRoleSeeding(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _seedCallbacks.RegisterRoleSeedCallback(callback);
        return this;
    }

    public IdentityBaseBuilder AfterIdentitySeed(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _seedCallbacks.RegisterIdentitySeedCallback(callback);
        return this;
    }

    public IdentityBaseBuilder AddUserLifecycleListener<TListener>() where TListener : class, IUserLifecycleListener
    {
        Services.AddScoped<IUserLifecycleListener, TListener>();
        return this;
    }

    public IdentityBaseBuilder AddNotificationContextAugmentor<TContext, TAugmentor>()
        where TContext : NotificationContext
        where TAugmentor : class, INotificationContextAugmentor<TContext>
    {
        Services.AddScoped<INotificationContextAugmentor<TContext>, TAugmentor>();
        return this;
    }

    [Obsolete("Use AddUserLifecycleListener instead.")]
    public IdentityBaseBuilder AddUserCreationListener<TListener>() where TListener : class, IUserCreationListener
    {
        Services.AddScoped<IUserCreationListener, TListener>();
        return this;
    }

    [Obsolete("Use AddUserLifecycleListener instead.")]
    public IdentityBaseBuilder AddUserUpdateListener<TListener>() where TListener : class, IUserUpdateListener
    {
        Services.AddScoped<IUserUpdateListener, TListener>();
        return this;
    }

    [Obsolete("Use AddUserLifecycleListener instead.")]
    public IdentityBaseBuilder AddUserDeletionListener<TListener>() where TListener : class, IUserDeletionListener
    {
        Services.AddScoped<IUserDeletionListener, TListener>();
        return this;
    }

    [Obsolete("Use AddUserLifecycleListener instead.")]
    public IdentityBaseBuilder AddUserRestoreListener<TListener>() where TListener : class, IUserRestoreListener
    {
        Services.AddScoped<IUserRestoreListener, TListener>();
        return this;
    }

    private void RegisterTenantContextAccessor()
    {
        Services.TryAddSingleton<ITenantContextAccessor, NullTenantContextAccessor>();
        Services.TryAddScoped<ITenantContext>(static sp => sp.GetRequiredService<ITenantContextAccessor>().Current);
    }

    public IdentityBaseBuilder AddConfiguredExternalProviders()
    {
        if (ProviderFlags.GoogleEnabled)
        {
            AddGoogleAuth();
        }

        if (ProviderFlags.MicrosoftEnabled)
        {
            AddMicrosoftAuth();
        }

        if (ProviderFlags.AppleEnabled)
        {
            AddAppleAuth();
        }

        return this;
    }

    public IdentityBaseBuilder AddGoogleAuth(
        string scheme = GoogleDefaults.AuthenticationScheme,
        Action<GoogleOptions>? configure = null)
    {
        if (!ProviderFlags.GoogleEnabled)
        {
            return this;
        }

        Services.AddOptions<GoogleOptions>().Configure<IOptions<ExternalProviderOptions>>((options, providerOptions) =>
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

        AuthenticationBuilder.AddGoogle(scheme, options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.SaveTokens = true;
            configure?.Invoke(options);
        });

        return this;
    }

    public IdentityBaseBuilder AddMicrosoftAuth(
        string scheme = MicrosoftAccountDefaults.AuthenticationScheme,
        Action<MicrosoftAccountOptions>? configure = null)
    {
        if (!ProviderFlags.MicrosoftEnabled)
        {
            return this;
        }

        Services.AddOptions<MicrosoftAccountOptions>().Configure<IOptions<ExternalProviderOptions>>((options, providerOptions) =>
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

        AuthenticationBuilder.AddMicrosoftAccount(scheme, options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.SaveTokens = true;
            configure?.Invoke(options);
        });

        return this;
    }

    public IdentityBaseBuilder AddAppleAuth(
        string scheme = ExternalAuthenticationConstants.AppleScheme,
        Action<OpenIdConnectOptions>? configure = null)
    {
        if (!ProviderFlags.AppleEnabled)
        {
            return this;
        }

        Services.AddOptions<OpenIdConnectOptions>(scheme)
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

        AuthenticationBuilder.AddOpenIdConnect(scheme, options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.SaveTokens = true;
            options.UsePkce = true;
            options.Authority = "https://appleid.apple.com";
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.ResponseMode = OpenIdConnectResponseMode.FormPost;
            options.CallbackPath = "/signin-apple";
            options.Scope.Clear();
            configure?.Invoke(options);
        });

        return this;
    }

    public IdentityBaseBuilder AddExternalAuthProvider(
        string scheme,
        Func<AuthenticationBuilder, AuthenticationBuilder> addScheme,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentNullException.ThrowIfNull(addScheme);

        configureServices?.Invoke(Services);
        AuthenticationBuilder = addScheme(AuthenticationBuilder);
        return this;
    }

    internal void ApplyConfigurationOverrides()
    {
        if (_options.UseDefaultOptionBinding)
        {
            DefaultOptionsConfigurator.Configure(Services, Configuration);
        }

        foreach (var action in _options.OptionConfigurators)
        {
            action(Services, Configuration);
        }
    }


    private ExternalProviderFlags ConfigureOptions()
    {
        ApplyConfigurationOverrides();

        var externalProvidersSection = Configuration.GetSection(ExternalProviderOptions.SectionName);
        var googleEnabled = externalProvidersSection.GetSection(nameof(ExternalProviderOptions.Google)).GetValue<bool?>(nameof(OAuthProviderOptions.Enabled)) ?? false;
        var microsoftEnabled = externalProvidersSection.GetSection(nameof(ExternalProviderOptions.Microsoft)).GetValue<bool?>(nameof(OAuthProviderOptions.Enabled)) ?? false;
        var appleEnabled = externalProvidersSection.GetSection(nameof(ExternalProviderOptions.Apple)).GetValue<bool?>(nameof(OAuthProviderOptions.Enabled)) ?? false;

        return new ExternalProviderFlags(googleEnabled, microsoftEnabled, appleEnabled);
    }

    private void ConfigureDatabase()
    {
        if (_configureAppDbContext is not null)
        {
            Services.AddDbContext<AppDbContext>((provider, options) =>
            {
                _configureAppDbContext(provider, options);
                AttachModelCustomization(options);
            });
            return;
        }

        EnsureDbContextRegistered<AppDbContext>(nameof(ServiceCollectionExtensions.AddIdentityBase));
    }

    private void ConfigureIdentity()
    {
        Services
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

        Services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(3);
        });
    }

    private void RegisterHostedServices()
    {
        Services.AddScoped<IdentityDataSeeder>();
        Services.AddHostedService<IdentitySeedHostedService>();
        Services.AddScoped<OpenIddictSeeder>();
        Services.AddHostedService<OpenIddictSeederHostedService>();
    }

    private void ConfigureCorsAndHttpClients()
    {
        Services.AddCors();
        Services.AddSingleton<IConfigureOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>, CorsPolicyConfigurator>();
        Services.AddHttpClient();
    }

    private void ConfigureOpenIddict()
    {
        Services.AddOpenIddict()
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
                    .SetUserInfoEndpointUris("/connect/userinfo")
                       .SetIntrospectionEndpointUris("/connect/introspect");

	                options.AllowAuthorizationCodeFlow()
	                       .AllowClientCredentialsFlow()
	                       .AllowRefreshTokenFlow();

                options.RequireProofKeyForCodeExchange();

                options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.OfflineAccess);

                options.UseConfiguredServerKeys(Configuration, Environment);

                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableStatusCodePagesIntegration();

	                options.AddEventHandler(ClientCredentialsGrantHandler.Descriptor);
	                options.AddEventHandler(ClientCredentialsFlowValidator.Descriptor);
	                options.AddEventHandler(AuthorizationCodeAugmentorHandler.Descriptor);
	                options.AddEventHandler(RefreshTokenAugmentorHandler.Descriptor);
	            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });
    }

    private void ConfigureAuthentication()
    {
        AuthenticationBuilder = Services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        AuthenticationBuilder.AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.None;
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

        AuthenticationBuilder.AddCookie(IdentityConstants.ExternalScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });

        AuthenticationBuilder.AddCookie(IdentityConstants.TwoFactorUserIdScheme, options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.None;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });

        AuthenticationBuilder.AddCookie(IdentityConstants.TwoFactorRememberMeScheme);
    }

    private void RegisterValidators()
    {
        Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        Services.AddScoped<IValidator<ConfirmEmailRequest>, ConfirmEmailRequestValidator>();
        Services.AddScoped<IValidator<ResendConfirmationRequest>, ResendConfirmationRequestValidator>();
        Services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        Services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        Services.AddScoped<IValidator<MfaVerifyRequest>, MfaVerifyRequestValidator>();
        Services.AddScoped<IValidator<RegisterUserRequest>, RegisterUserRequestValidator>();
        Services.AddScoped<IValidator<MfaChallengeRequest>, MfaChallengeRequestValidator>();
        Services.AddScoped<IValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
        Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
    }

    private void RegisterApplicationServices()
    {
        Services.AddSingleton<ILogSanitizer, LogSanitizer>();
        Services.AddSingleton<IExternalReturnUrlValidator, ExternalReturnUrlValidator>();
        Services.AddSingleton<IExternalCallbackUriFactory, ExternalCallbackUriFactory>();
        Services.TryAddScoped<ITemplatedEmailSender, NoOpTemplatedEmailSender>();
        Services.AddScoped<IAccountEmailService, AccountEmailService>();
        Services.TryAddScoped(typeof(INotificationContextPipeline<>), typeof(NotificationContextPipeline<>));
        Services.AddScoped<ExternalAuthenticationService>();
        Services.AddScoped<IAuditLogger, AuditLogger>();
        Services.AddOptions<LifecycleHookOptions>();
        Services.AddScoped<IUserLifecycleHookDispatcher, UserLifecycleHookDispatcher>();
        Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserLifecycleListener, LegacyUserLifecycleListener>());
    }

    private void ConfigureMfaChallengeSenders()
    {
        Services.AddScoped<IMfaChallengeSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MfaOptions>>().Value;
            return options.Email.Enabled
                ? ActivatorUtilities.CreateInstance<EmailMfaChallengeSender>(provider)
                : ActivatorUtilities.CreateInstance<DisabledMfaChallengeSender>(
                    provider,
                    "email",
                    "Email MFA challenge is disabled.");
        });

        Services.AddScoped<IMfaChallengeSender>(provider =>
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

    private void ConfigureHealthChecks()
    {
        Services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck<ExternalProvidersHealthCheck>("externalProviders");
    }

    private void AttachModelCustomization(DbContextOptionsBuilder options)
    {
        ((IDbContextOptionsBuilderInfrastructure)options)
            .AddOrUpdateExtension(new IdentityBaseModelCustomizationOptionsExtension(_modelCustomizationOptions));
    }

    private void EnsureDbContextRegistered<TContext>(string registrationSource)
        where TContext : DbContext
    {
        var hasRegistration = Services.Any(descriptor =>
            descriptor.ServiceType == typeof(DbContextOptions<TContext>) ||
            descriptor.ServiceType == typeof(DbContextOptions));

        if (!hasRegistration)
        {
            throw new InvalidOperationException(
                $"No DbContext registration found for {typeof(TContext).Name}. " +
                $"Register it before calling {registrationSource} or pass a configureDbContext delegate.");
        }
    }

    internal readonly record struct ExternalProviderFlags(bool GoogleEnabled, bool MicrosoftEnabled, bool AppleEnabled)
    {
        public static readonly ExternalProviderFlags None = new(false, false, false);
    }

    private static class DefaultOptionsConfigurator
    {
        public static void Configure(IServiceCollection services, IConfiguration configuration)
        {
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
            services.AddSingleton<IValidateOptions<MfaOptions>, MfaOptionsValidator>();
            services.AddSingleton<IValidateOptions<ExternalProviderOptions>, ExternalProviderOptionsValidator>();
            services.AddSingleton<IValidateOptions<OpenIddictOptions>, OpenIddictOptionsValidator>();
            services.AddSingleton<IValidateOptions<OpenIddictServerKeyOptions>, OpenIddictServerKeyOptionsValidator>();
            services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();
        }
    }

    internal IServiceCollection GetServices() => Services;

    internal IConfiguration GetConfiguration() => Configuration;

}
