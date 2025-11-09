using System;
using Identity.Base.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Extensions;

public static class ServiceCollectionExtensions
{
    public static IdentityBaseBuilder AddIdentityBase(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        Action<IdentityBaseOptions>? configure = null,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContext = null)
    {
        var options = new IdentityBaseOptions();
        configure?.Invoke(options);

        var builder = new IdentityBaseBuilder(services, configuration, environment, options, configureDbContext);
        return builder.Initialize();
    }
}
