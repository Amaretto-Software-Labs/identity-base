using System;
using Identity.Base.Abstractions.MultiTenancy;
using Identity.Base.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Identity.Base.Tests;

public class TenantContextDefaultsTests
{
    [Fact]
    public void AddIdentityBase_RegistersNullTenantContextAccessor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var environment = new FakeWebHostEnvironment();

        services.AddIdentityBase(configuration, environment);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var context = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        Assert.IsType<NullTenantContextAccessor>(accessor);
        Assert.Same(accessor.Current, context);
        Assert.False(context.HasTenant);
        Assert.Null(context.TenantId);
        Assert.Null(context.TenantKey);
        Assert.Null(context.DisplayName);

        using var tenantScope = accessor.BeginScope(new TenantContext(Guid.NewGuid(), "alpha", "Alpha"));
        // Null accessor should ignore overrides and continue surfacing the empty context.
        Assert.Same(TenantContext.None, accessor.Current);
        Assert.False(accessor.Current.HasTenant);
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
