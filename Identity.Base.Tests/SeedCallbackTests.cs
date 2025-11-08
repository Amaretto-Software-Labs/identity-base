using System.Collections.Generic;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Roles;
using Identity.Base.Roles.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Identity.Base.Tests;

public class SeedCallbackTests
{
    [Fact]
    public async Task AfterRoleSeeding_CallbackInvoked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = BuildConfiguration("role-seed-callback");
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new FakeWebHostEnvironment();

        var dbName = "role-seed-callback";
        var configureDb = new Action<IServiceProvider, DbContextOptionsBuilder>((_, options) =>
            options.UseInMemoryDatabase(dbName)
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

        var identityBuilder = services.AddIdentityBase(
            configuration,
            environment,
            configureDbContext: configureDb);
        var callbackInvoked = false;
        identityBuilder.AfterRoleSeeding((_, _) =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        services.AddIdentityRoles(configuration, configureDb);

        using var provider = services.BuildServiceProvider();
        await provider.SeedIdentityRolesAsync();

        Assert.True(callbackInvoked);
    }

    [Fact]
    public async Task AfterIdentitySeed_CallbackInvoked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = BuildConfiguration("identity-seed-callback");
        configuration["IdentitySeed:Enabled"] = "true";
        configuration["IdentitySeed:Email"] = "seed@example.com";
        configuration["IdentitySeed:Password"] = "P@ssword12345!";
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new FakeWebHostEnvironment();

        var configureDb = new Action<IServiceProvider, DbContextOptionsBuilder>((_, options) =>
            options.UseInMemoryDatabase("identity-seed-callback")
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

        var identityBuilder = services.AddIdentityBase(
            configuration,
            environment,
            configureDbContext: configureDb);
        var callbackInvoked = false;
        identityBuilder.AfterIdentitySeed((_, _) =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
        await seeder.SeedAsync();

        Assert.True(callbackInvoked);
    }

    private static IConfiguration BuildConfiguration(string databaseName)
    {
        var data = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Primary"] = $"InMemory:{databaseName}",
            ["MailJet:ApiKey"] = "test-key",
            ["MailJet:ApiSecret"] = "test-secret",
            ["MailJet:FromEmail"] = "no-reply@example.com",
            ["MailJet:Templates:Confirmation"] = "1",
            ["MailJet:Templates:PasswordReset"] = "2",
            ["MailJet:Templates:MfaChallenge"] = "3",
            ["OpenIddict:Applications:0:ClientId"] = "test-client"
        };

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
}
