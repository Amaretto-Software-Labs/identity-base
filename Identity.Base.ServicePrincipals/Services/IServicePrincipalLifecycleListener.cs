namespace Identity.Base.ServicePrincipals.Services;

public interface IServicePrincipalLifecycleListener
{
    /// <summary>
    /// Runs before a service principal is disabled.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown to reject the disable operation with an HTTP 409 conflict response.
    /// </exception>
    Task BeforeDisableAsync(Guid servicePrincipalId, CancellationToken cancellationToken);
}
