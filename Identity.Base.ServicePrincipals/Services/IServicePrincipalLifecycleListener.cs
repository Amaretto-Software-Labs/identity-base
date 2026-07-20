namespace Identity.Base.ServicePrincipals.Services;

public interface IServicePrincipalLifecycleListener
{
    Task BeforeDisableAsync(Guid servicePrincipalId, CancellationToken cancellationToken);
}
