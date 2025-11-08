using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Identity.Base.Admin.Configuration;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Options;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Services;
using Identity.Base.Roles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Identity.Base.Tests;

public class IdentitySeedTests
{
    [Fact]
    public async Task SeedAsync_CreatesAdminUser_WithRbacAssignments()
    {
        var databaseName = $"identity-seed-{Guid.NewGuid():N}";

        var configuration = BuildConfiguration(databaseName);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new FakeWebHostEnvironment();

        var configureIdentityDb = new Action<IServiceProvider, DbContextOptionsBuilder>((_, options) =>
            options.UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        var configureRolesDb = new Action<IServiceProvider, DbContextOptionsBuilder>((_, options) =>
            options.UseInMemoryDatabase($"{databaseName}_roles")
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

        services.AddIdentityBase(configuration, environment, configureDbContext: configureIdentityDb);
        services.AddIdentityAdmin(configuration, configureRolesDb);

        using var provider = services.BuildServiceProvider();
        await provider.SeedIdentityRolesAsync();

        using var scope = provider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
        await seeder.SeedAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("seed-admin@example.com");
        user.ShouldNotBeNull();

        var identityRoles = await userManager.GetRolesAsync(user!);
        identityRoles.ShouldContain("IdentityAdmin");

        var roleAssignment = scope.ServiceProvider.GetRequiredService<IRoleAssignmentService>();
        var rbacRoles = await roleAssignment.GetUserRoleNamesAsync(user!.Id);
        rbacRoles.ShouldContain("IdentityAdmin");

        var permissionResolver = scope.ServiceProvider.GetRequiredService<IPermissionResolver>();
        var permissions = await permissionResolver.GetEffectivePermissionsAsync(user.Id);
        permissions.ShouldContain("users.create");
        permissions.ShouldContain("roles.manage");
        permissions.ShouldContain("admin.organizations.manage");
    }

    [Fact]
    public async Task SeededAdminToken_ContainsAllRolePermissions()
    {
        using var factory = new SeededIdentityApiFactory();

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.SeedIdentityRolesAsync();
            var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
            await seeder.SeedAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.GrantType] = OpenIddictConstants.GrantTypes.Password,
            [OpenIddictConstants.Parameters.Username] = "seed-admin@example.com",
            [OpenIddictConstants.Parameters.Password] = "P@ssword12345!",
            [OpenIddictConstants.Parameters.ClientId] = "test-client",
            [OpenIddictConstants.Parameters.ClientSecret] = "test-secret",
            [OpenIddictConstants.Parameters.Scope] = "openid profile email identity.api identity.admin"
        });

        using var tokenResponse = await client.PostAsync("/connect/token", tokenRequest);
        var payload = await tokenResponse.Content.ReadAsStringAsync();
        tokenResponse.IsSuccessStatusCode.ShouldBeTrue(payload);

        var json = JsonDocument.Parse(payload);
        var accessToken = json.RootElement.GetProperty("access_token").GetString();
        accessToken.ShouldNotBeNullOrWhiteSpace();

        var parts = accessToken!.Split('.');
        parts.Length.ShouldBeGreaterThanOrEqualTo(2);
        var jwtPayload = Encoding.UTF8.GetString(JwtTestUtilities.Base64UrlDecode(parts[1]));
        using var tokenJson = JsonDocument.Parse(jwtPayload);

        tokenJson.RootElement.TryGetProperty("identity.permissions", out var permissionsElement).ShouldBeTrue();
        var permissionString = permissionsElement.GetString();
        permissionString.ShouldNotBeNull();

        var permissionSet = permissionString!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expectedPermissions = new[]
        {
            "users.read",
            "users.create",
            "users.update",
            "users.lock",
            "users.reset-password",
            "users.reset-mfa",
            "users.manage-roles",
            "users.delete",
            "roles.read",
            "roles.manage",
            "admin.organizations.read",
            "admin.organizations.manage",
            "admin.organizations.members.read",
            "admin.organizations.members.manage",
            "admin.organizations.roles.read",
            "admin.organizations.roles.manage"
        };

        foreach (var permission in expectedPermissions)
        {
            permissionSet.ShouldContain(permission);
        }
    }

    private static IConfiguration BuildConfiguration(string databaseName)
    {
        var data = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Primary"] = $"InMemory:{databaseName}",
            ["IdentitySeed:Enabled"] = "true",
            ["IdentitySeed:Email"] = "seed-admin@example.com",
            ["IdentitySeed:Password"] = "P@ssword12345!",
            ["IdentitySeed:Roles:0"] = "IdentityAdmin",

            ["Permissions:Definitions:0:Name"] = "users.read",
            ["Permissions:Definitions:0:Description"] = "View user directory",
            ["Permissions:Definitions:1:Name"] = "users.create",
            ["Permissions:Definitions:2:Name"] = "users.update",
            ["Permissions:Definitions:3:Name"] = "users.lock",
            ["Permissions:Definitions:4:Name"] = "users.reset-password",
            ["Permissions:Definitions:5:Name"] = "users.reset-mfa",
            ["Permissions:Definitions:6:Name"] = "users.manage-roles",
            ["Permissions:Definitions:7:Name"] = "users.delete",
            ["Permissions:Definitions:8:Name"] = "roles.read",
            ["Permissions:Definitions:9:Name"] = "roles.manage",
            ["Permissions:Definitions:10:Name"] = "admin.organizations.read",
            ["Permissions:Definitions:11:Name"] = "admin.organizations.manage",
            ["Permissions:Definitions:12:Name"] = "admin.organizations.members.read",
            ["Permissions:Definitions:13:Name"] = "admin.organizations.members.manage",
            ["Permissions:Definitions:14:Name"] = "admin.organizations.roles.read",
            ["Permissions:Definitions:15:Name"] = "admin.organizations.roles.manage"
        };

        var identityAdminBaseIndex = 0;
        data[$"Roles:Definitions:{identityAdminBaseIndex}:Name"] = "IdentityAdmin";
        data[$"Roles:Definitions:{identityAdminBaseIndex}:Description"] = "Full administrative access";
        var identityAdminPermissions = new[]
        {
            "users.read",
            "users.create",
            "users.update",
            "users.lock",
            "users.reset-password",
            "users.reset-mfa",
            "users.manage-roles",
            "users.delete",
            "roles.read",
            "roles.manage",
            "admin.organizations.read",
            "admin.organizations.manage",
            "admin.organizations.members.read",
            "admin.organizations.members.manage",
            "admin.organizations.roles.read",
            "admin.organizations.roles.manage"
        };

        for (var index = 0; index < identityAdminPermissions.Length; index++)
        {
            data[$"Roles:Definitions:{identityAdminBaseIndex}:Permissions:{index}"] = identityAdminPermissions[index];
        }

        data["Roles:DefaultAdminRoles:0"] = "IdentityAdmin";

        var standardUserIndex = 1;
        data[$"Roles:Definitions:{standardUserIndex}:Name"] = "StandardUser";
        data[$"Roles:Definitions:{standardUserIndex}:Description"] = "Default member";
        data[$"Roles:Definitions:{standardUserIndex}:IsSystemRole"] = "false";
        data["Roles:DefaultUserRoles:0"] = "StandardUser";

        return new ConfigurationBuilder().AddInMemoryCollection(data!).Build();
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Identity.Base.Tests";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class SeededIdentityApiFactory : IdentityApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["IdentitySeed:Enabled"] = "true",
                    ["IdentitySeed:Email"] = "seed-admin@example.com",
                    ["IdentitySeed:Password"] = "P@ssword12345!",
                    ["IdentitySeed:Roles:0"] = "IdentityAdmin"
                };

                configurationBuilder.AddInMemoryCollection(overrides);
            });

            base.ConfigureWebHost(builder);
        }
    }
}
