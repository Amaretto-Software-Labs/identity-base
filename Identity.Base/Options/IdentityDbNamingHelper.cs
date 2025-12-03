using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

internal static class IdentityDbNamingHelper
{
    public static string ResolveTablePrefix(DbContext context)
    {
        // Try to get from the application service provider (set via UseApplicationServiceProvider)
        // The application service provider is stored in CoreOptionsExtension
        var dbContextOptions = context.GetService<IDbContextOptions>();
        var coreExtension = dbContextOptions?.FindExtension<CoreOptionsExtension>();
        var applicationServiceProvider = coreExtension?.ApplicationServiceProvider;

        var options = applicationServiceProvider?.GetService<IOptions<IdentityDbNamingOptions>>();
        if (options is not null)
        {
            return IdentityDbNamingOptions.Normalize(options.Value?.TablePrefix);
        }

        // Fallback to internal service provider (for design-time with UseInternalServiceProvider)
        var internalServiceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        options = internalServiceProvider?.GetService<IOptions<IdentityDbNamingOptions>>();
        return IdentityDbNamingOptions.Normalize(options?.Value?.TablePrefix);
    }

    public static string Table(string prefix, string name)
        => $"{prefix}_{name}";

    public static string Index(string prefix, string name)
        => $"IX_{prefix}_{name}";

    public static string PrimaryKey(string prefix, string name)
        => $"PK_{prefix}_{name}";
}
