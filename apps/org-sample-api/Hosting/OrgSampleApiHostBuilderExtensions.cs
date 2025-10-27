using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Identity.Base.Abstractions;
using Identity.Base.Admin.Configuration;
using Identity.Base.Admin.Endpoints;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Options;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Extensions;
using Identity.Base.Roles;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Endpoints;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrgSampleApi.Sample.Data;
using OrgSampleApi.Sample.Invitations;
using OrgSampleApi.Sample.Members;
using OrgSampleApi.Hosting.Infrastructure;
using Microsoft.Extensions.Hosting;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace OrgSampleApi.Hosting;

internal static class OrgSampleApiHostBuilderExtensions
{
    public static void AddOrgSampleServices(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

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

        var services = builder.Services;
        var configuration = builder.Configuration;

        services.Configure<OrganizationBootstrapOptions>(configuration.GetSection("SampleData:DefaultOrganization"));

        services.AddScoped<OrganizationBootstrapService>();
        services.AddScoped<IUserCreationListener, OrganizationBootstrapUserCreationListener>();

        services.AddDbContext<OrgSampleDbContext>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var connectionString = GetPrimaryConnectionString(config);
            ConfigureDatabase(options, connectionString);
        });

        services.AddScoped<IInvitationStore, EfInvitationStore>();
        services.AddScoped<InvitationService>();
        services.AddScoped<OrganizationMemberDirectory>();
        services.AddHostedService<OrgSampleMigrationHostedService>();

        var identityBuilder = services.AddIdentityBase(configuration, builder.Environment);
        identityBuilder.AddConfiguredExternalProviders();

        var rolesBuilder = services.AddIdentityAdmin(configuration);
        rolesBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var connectionString = GetPrimaryConnectionString(config);
            ConfigureDatabase(options, connectionString);
        });

        var organizationsBuilder = services.AddIdentityBaseOrganizations();
        organizationsBuilder.AddDbContext<OrganizationDbContext>((provider, options) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var connectionString = GetPrimaryConnectionString(config);
            ConfigureDatabase(options, connectionString);
        });

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

    public static ILogger UseOrgSampleLifecycleLogging(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("OrgSample.Startup");
        var configuredUrls = string.Join(", ", app.Urls);
        var envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

        logger.LogInformation(
            "Org sample API initialized. Environment: {Environment}. URL configuration: {Urls}. ASPNETCORE_URLS: {EnvironmentUrls}",
            app.Environment.EnvironmentName,
            string.IsNullOrWhiteSpace(configuredUrls) ? "<none>" : configuredUrls,
            string.IsNullOrWhiteSpace(envUrls) ? "<unset>" : envUrls);

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var server = app.Services.GetService<IServer>();
            var addressFeature = server?.Features.Get<IServerAddressesFeature>();
            var resolvedUrls = addressFeature?.Addresses?.Any() == true
                ? string.Join(", ", addressFeature.Addresses)
                : string.Join(", ", app.Urls);

            logger.LogInformation(
                "Org sample API is listening on: {Urls}",
                string.IsNullOrWhiteSpace(resolvedUrls) ? "<none>" : resolvedUrls);
        });

        app.Lifetime.ApplicationStopping.Register(() =>
            logger.LogInformation("Org sample API shutting down."));

        app.Lifetime.ApplicationStopped.Register(() =>
            logger.LogInformation("Org sample API stopped."));

        return logger;
    }

    public static void ConfigureOrgSamplePipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseApiPipeline();

        app.MapControllers();
        app.MapApiEndpoints();
        app.MapIdentityAdminEndpoints();
        app.MapIdentityRolesUserEndpoints();
        app.MapIdentityBaseOrganizationEndpoints();

        MapSampleEndpoints(app);
    }

    private static void MapSampleEndpoints(WebApplication app)
    {
        var sampleGroup = app.MapGroup("/sample")
            .WithTags("Sample");

        sampleGroup.MapGet("/status", () => Results.Ok(new
        {
            Message = "Organization sample API is running.",
            Timestamp = DateTimeOffset.UtcNow
        }));

        sampleGroup.MapGet("/defaults", (IOptions<OrganizationBootstrapOptions> options) =>
        {
            var defaults = options.Value;
            return Results.Ok(new
            {
                defaults.Slug,
                defaults.DisplayName,
                defaults.Metadata
            });
        });

        sampleGroup.MapGet("/registration/profile-fields", (IOptions<RegistrationOptions> options) =>
        {
            var registration = options.Value;
            var fields = registration.ProfileFields.Select(field => new
            {
                field.Name,
                field.DisplayName,
                field.Required,
                field.MaxLength,
                field.Pattern
            });

            return Results.Ok(new { fields });
        });

        sampleGroup.MapGet("/organizations/{organizationId:guid}/members", async (
            Guid organizationId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            OrganizationMemberDirectory memberDirectory,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var members = await memberDirectory.GetMembersAsync(organizationId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(members.Select(member => new
            {
                member.OrganizationId,
                member.UserId,
                member.IsPrimary,
                member.RoleIds,
                member.CreatedAtUtc,
                member.UpdatedAtUtc,
                member.Email,
                member.DisplayName
            }));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.read"));

        sampleGroup.MapPatch("/organizations/{organizationId:guid}/members/{userId:guid}", async (
            Guid organizationId,
            Guid userId,
            UpdateOrganizationMemberRequest request,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationMembershipService membershipService,
            OrganizationMemberDirectory memberDirectory,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid update", Detail = "Request body is required.", Status = StatusCodes.Status400BadRequest });
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            if (!TryGetUserId(principal, out var actorUserId))
            {
                return Results.Unauthorized();
            }

            if (actorUserId == userId)
            {
                return Results.Problem(new ProblemDetails
                {
                    Title = "Cannot modify self",
                    Detail = "You cannot change your own organization membership via this endpoint.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            if (request.RoleIds is null && !request.IsPrimary.HasValue)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid update",
                    Detail = "Specify roles or primary status to update.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            try
            {
                await membershipService.UpdateMembershipAsync(new OrganizationMembershipUpdateRequest
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    RoleIds = request.RoleIds?.ToArray(),
                    IsPrimary = request.IsPrimary
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid membership update", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organization membership could not be found.", Status = StatusCodes.Status404NotFound });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Membership conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }

            var updated = await memberDirectory.GetMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
            if (updated is null)
            {
                return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organization membership could not be found.", Status = StatusCodes.Status404NotFound });
            }

            return Results.Ok(new
            {
                updated.OrganizationId,
                updated.UserId,
                updated.IsPrimary,
                updated.RoleIds,
                updated.CreatedAtUtc,
                updated.UpdatedAtUtc,
                updated.Email,
                updated.DisplayName
            });
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        sampleGroup.MapDelete("/organizations/{organizationId:guid}/members/{userId:guid}", async (
            Guid organizationId,
            Guid userId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            if (!TryGetUserId(principal, out var actorUserId))
            {
                return Results.Unauthorized();
            }

            if (actorUserId == userId)
            {
                return Results.Problem(new ProblemDetails
                {
                    Title = "Cannot remove self",
                    Detail = "You cannot remove your own membership from the organization.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            var membership = await membershipService.GetMembershipAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
            if (membership is null)
            {
                return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = "The organization membership could not be found.", Status = StatusCodes.Status404NotFound });
            }

            await membershipService.RemoveMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        sampleGroup.MapGet("/organizations/{organizationId:guid}/invitations", async (
            Guid organizationId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            InvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var invitations = await invitationService.ListAsync(organizationId, cancellationToken).ConfigureAwait(false);
            var response = invitations.Select(invitation => new InvitationResponse
            {
                Code = invitation.Code,
                Email = invitation.Email,
                RoleIds = invitation.RoleIds,
                ExpiresAtUtc = invitation.ExpiresAtUtc
            }).ToList();

            return Results.Ok(response);
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        sampleGroup.MapPost("/organizations/{organizationId:guid}/invitations", async (
            Guid organizationId,
            CreateInvitationRequest request,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            InvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email is required."] });
            }

            var emailAttribute = new EmailAddressAttribute();
            if (!emailAttribute.IsValid(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email format is invalid."] });
            }

            var actorUserId = TryGetUserId(principal, out var userId) ? userId : (Guid?)null;

            try
            {
                var invitation = await invitationService.CreateAsync(
                    organizationId,
                    request.Email,
                    request.RoleIds ?? Array.Empty<Guid>(),
                    actorUserId,
                    request.ExpiresInHours,
                    cancellationToken).ConfigureAwait(false);

                var response = new InvitationResponse
                {
                    Code = invitation.Code,
                    Email = invitation.Email,
                    RoleIds = invitation.RoleIds,
                    ExpiresAtUtc = invitation.ExpiresAtUtc
                };

                return Results.Created($"/sample/organizations/{organizationId}/invitations/{invitation.Code}", response);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = new[] { ex.Message } });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        sampleGroup.MapDelete("/organizations/{organizationId:guid}/invitations/{code:guid}", async (
            Guid organizationId,
            Guid code,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            InvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var revoked = await invitationService.RevokeAsync(organizationId, code, cancellationToken).ConfigureAwait(false);
            return revoked ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission("organization.members.manage"));

        sampleGroup.MapPost("/invitations/claim", async (
            ClaimInvitationRequest request,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            InvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            if (request.Code == Guid.Empty)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = ["Invitation code is required."] });
            }

            var user = await userManager.GetUserAsync(principal).ConfigureAwait(false);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await invitationService.AcceptAsync(request.Code, user, cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(new
                {
                    result.OrganizationId,
                    result.OrganizationSlug,
                    result.OrganizationName,
                    result.RoleIds,
                    result.WasExistingMember,
                    RequiresTokenRefresh = true
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { Message = ex.Message });
            }
        })
        .RequireAuthorization();
    }

    private static string GetPrimaryConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Primary");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Primary must be configured.");
        }

        return connectionString;
    }

    private static void ConfigureDatabase(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure());
    }

    private static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        if (organizationId == Guid.Empty)
        {
            return Results.BadRequest(new Dictionary<string, string[]> { ["organizationId"] = ["Organization identifier is required."] });
        }

        var inScope = await scopeResolver.IsInScopeAsync(userId, organizationId, cancellationToken).ConfigureAwait(false);
        if (!inScope)
        {
            return Results.Forbid();
        }

        return null;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
