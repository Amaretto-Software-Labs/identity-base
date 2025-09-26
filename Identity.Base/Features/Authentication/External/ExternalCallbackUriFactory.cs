using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Identity.Base.Features.Authentication.External;

public interface IExternalCallbackUriFactory
{
    string Create(HttpContext context, string provider);
}

public sealed class ExternalCallbackUriFactory : IExternalCallbackUriFactory
{
    public string Create(HttpContext context, string provider)
    {
        var request = context.Request;
        var scheme = request.Scheme;
        var host = request.Host;
        if (!host.HasValue)
        {
            host = new HostString("localhost");
        }

        var safeProvider = Uri.EscapeDataString(provider);
        var callbackPath = new PathString($"/auth/external/{safeProvider}/callback");
        var fullPath = request.PathBase.Add(callbackPath);

        return UriHelper.BuildAbsolute(scheme, host, fullPath);
    }
}
