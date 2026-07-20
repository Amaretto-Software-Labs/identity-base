using Identity.Base.ServicePrincipals.Data;
using Identity.Base.ServicePrincipals.Services;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Identity.Base.ServicePrincipals.OpenIddict;

internal sealed class ServicePrincipalClientSecretValidator(
    ServicePrincipalDbContext dbContext,
    ServicePrincipalService service,
    IOpenIddictApplicationManager applicationManager)
    : IOpenIddictServerHandler<ProcessAuthenticationContext>
{
    public async ValueTask HandleAsync(ProcessAuthenticationContext context)
    {
        if (string.IsNullOrEmpty(context.ClientId) || string.IsNullOrEmpty(context.ClientSecret) ||
            context.EndpointType is OpenIddictServerEndpointType.Authorization
                or OpenIddictServerEndpointType.EndSession
                or OpenIddictServerEndpointType.EndUserVerification
                or OpenIddictServerEndpointType.UserInfo)
        {
            return;
        }

        var managed = await dbContext.ServicePrincipals
            .AsNoTracking()
            .AnyAsync(item => item.ClientId == context.ClientId, context.CancellationToken);
        if (managed)
        {
            if (!await service.ValidateCredentialAsync(context.ClientId, context.ClientSecret, context.CancellationToken))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidClient, "Invalid client credentials.");
            }
            return;
        }

        var application = await applicationManager.FindByClientIdAsync(context.ClientId, context.CancellationToken)
            ?? throw new InvalidOperationException("The OpenIddict application could not be found.");
        if (!await applicationManager.HasClientTypeAsync(
                application, OpenIddictConstants.ClientTypes.Public, context.CancellationToken) &&
            !await applicationManager.ValidateClientSecretAsync(
                application, context.ClientSecret, context.CancellationToken))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "Invalid client credentials.");
        }
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
            .UseScopedHandler<ServicePrincipalClientSecretValidator>()
            .SetOrder(OpenIddictServerHandlers.ValidateClientSecret.Descriptor.Order)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
}
