using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OrgSampleApi.Hosting.Configuration;

internal static class WebApplicationExtensions
{
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
}
