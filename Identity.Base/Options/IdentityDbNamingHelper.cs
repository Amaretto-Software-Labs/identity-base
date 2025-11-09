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
        var serviceProvider = ((IInfrastructure<IServiceProvider>)context).Instance;
        var options = serviceProvider?.GetService<IOptions<IdentityDbNamingOptions>>();
        return IdentityDbNamingOptions.Normalize(options?.Value?.TablePrefix);
    }

    public static string Table(string prefix, string name)
        => $"{prefix}_{name}";

    public static string Index(string prefix, string name)
        => $"IX_{prefix}_{name}";

    public static string PrimaryKey(string prefix, string name)
        => $"PK_{prefix}_{name}";
}
