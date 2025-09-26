using System;
using System.Collections.Generic;
using System.Linq;
using Identity.Base.Options;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Base.OpenIddict.Handlers;

public sealed class PasswordFlowClientValidator : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    private readonly IOptions<OpenIddictOptions> _options;
    private HashSet<string>? _allowedClients;

    public PasswordFlowClientValidator(IOptions<OpenIddictOptions> options)
    {
        _options = options;
    }

    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        if (!context.Request.IsPasswordGrantType())
        {
            return default;
        }

        if (string.IsNullOrWhiteSpace(context.ClientId))
        {
            context.Reject(OpenIddictConstants.Errors.UnauthorizedClient, "Client must be registered to use the password grant.");
            return default;
        }

        var allowed = _allowedClients ??= BuildAllowedClients();
        if (!allowed.Contains(context.ClientId))
        {
            context.Reject(OpenIddictConstants.Errors.UnauthorizedClient, "Password grant is disabled for this client.");
        }

        return default;
    }

    private HashSet<string> BuildAllowedClients()
    {
        return _options.Value.Applications
            .Where(application => application.AllowPasswordFlow)
            .Select(application => application.ClientId)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
            .UseScopedHandler<PasswordFlowClientValidator>()
            .SetOrder(int.MinValue + 4000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}
