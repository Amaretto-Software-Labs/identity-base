using Identity.Base.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Identity.Base.Options;

internal sealed class CorsPolicyConfigurator : IConfigureOptions<CorsOptions>
{
    private readonly IOptions<CorsSettings> _settings;

    public CorsPolicyConfigurator(IOptions<CorsSettings> settings)
    {
        _settings = settings;
    }

    public void Configure(CorsOptions options)
    {
        var allowedOrigins = _settings.Value.AllowedOrigins.ToArray();

        options.AddPolicy(CorsSettings.PolicyName, builder =>
        {
            builder.WithOrigins(allowedOrigins)
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
    }
}
