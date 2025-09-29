using System;
using System.Collections.Generic;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Data;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Options;
using Identity.Base.Roles;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Identity.Base.Tests;

public class ModelCustomizationTests
{
    [Fact]
    public void AddIdentityBase_RegistersCustomizationOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = BuildConfiguration("model-options");
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new FakeWebHostEnvironment();

        services.AddIdentityBase(configuration, environment);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IdentityBaseModelCustomizationOptions>();

        Assert.Empty(options.AppDbContextCustomizations);
        Assert.Empty(options.IdentityRolesDbContextCustomizations);
    }

    [Fact]
    public void AppDbContext_Customization_IsInvoked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = BuildConfiguration("appdb-customization");
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new FakeWebHostEnvironment();

        var builder = services.AddIdentityBase(configuration, environment);
        var invoked = false;
        builder.ConfigureAppDbContextModel(model =>
        {
            invoked = true;
            model.Model.SetAnnotation("Identity.Base.Tests:TenantIndex", true);
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IdentityBaseModelCustomizationOptions>();
        Assert.Single(options.AppDbContextCustomizations);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var extension = dbContext.GetService<IDbContextOptions>()
            .FindExtension<IdentityBaseModelCustomizationOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Single(extension!.Options.AppDbContextCustomizations);

        var model = dbContext.Model;
        Assert.True(invoked);
        Assert.True((bool?)model.FindAnnotation("Identity.Base.Tests:TenantIndex")?.Value ?? false);
    }

    [Fact]
    public void IdentityRolesDbContext_Customization_IsInvoked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = BuildConfiguration("rolesdb-customization");
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new FakeWebHostEnvironment();

        var identityBuilder = services.AddIdentityBase(configuration, environment);
        var invoked = false;
        identityBuilder.ConfigureIdentityRolesModel(model =>
        {
            invoked = true;
            model.Model.SetAnnotation("Identity.Base.Tests:RoleTenantIndex", true);
        });

        var rolesBuilder = services.AddIdentityRoles(configuration);
        rolesBuilder.AddDbContext<IdentityRolesDbContext>(options => options.UseInMemoryDatabase("rolesdb-customization"));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IdentityBaseModelCustomizationOptions>();
        Assert.Single(options.IdentityRolesDbContextCustomizations);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityRolesDbContext>();
        var extension = dbContext.GetService<IDbContextOptions>()
            .FindExtension<IdentityBaseModelCustomizationOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Single(extension!.Options.IdentityRolesDbContextCustomizations);

        var model = dbContext.Model;
        Assert.True(invoked);
        Assert.True((bool?)model.FindAnnotation("Identity.Base.Tests:RoleTenantIndex")?.Value ?? false);
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

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
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
