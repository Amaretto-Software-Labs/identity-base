using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Identity.Base.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Security;

internal sealed class BrowserOriginGuardEndpointFilter : IEndpointFilter
{
    private readonly IOptions<CorsSettings> _corsSettings;
    private readonly ILogger<BrowserOriginGuardEndpointFilter> _logger;

    public BrowserOriginGuardEndpointFilter(IOptions<CorsSettings> corsSettings, ILogger<BrowserOriginGuardEndpointFilter> logger)
    {
        _corsSettings = corsSettings;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        var method = httpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return await next(context).ConfigureAwait(false);
        }

        var origin = httpContext.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            // Non-browser clients typically omit Origin. CSRF is a browser concern.
            return await next(context).ConfigureAwait(false);
        }

        if (!TryNormalizeOrigin(origin, out var normalizedOrigin))
        {
            _logger.LogWarning("Blocked request with invalid Origin header. Origin={Origin} Path={Path}", origin, httpContext.Request.Path);
            return Results.Problem("Invalid Origin header.", statusCode: StatusCodes.Status400BadRequest);
        }

        var requestOrigin = $"{httpContext.Request.Scheme}://{httpContext.Request.Host.Value}";
        if (TryNormalizeOrigin(requestOrigin, out var normalizedRequestOrigin) &&
            string.Equals(normalizedOrigin, normalizedRequestOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return await next(context).ConfigureAwait(false);
        }

        var allowedOrigins = BuildAllowedOrigins();
        if (allowedOrigins.Contains(normalizedOrigin))
        {
            return await next(context).ConfigureAwait(false);
        }

        _logger.LogWarning(
            "Blocked cross-origin request. Origin={Origin} Path={Path}",
            normalizedOrigin,
            httpContext.Request.Path);
        return Results.Problem("Cross-origin request blocked.", statusCode: StatusCodes.Status403Forbidden);
    }

    private HashSet<string> BuildAllowedOrigins()
    {
        var origins = _corsSettings.Value.AllowedOrigins ?? new List<string>();
        return origins
            .Select(o => TryNormalizeOrigin(o, out var normalized) ? normalized : null)
            .Where(o => o is not null)
            .Select(o => o!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeOrigin(string value, out string normalized)
    {
        normalized = string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
        return normalized.Length > 0;
    }
}

