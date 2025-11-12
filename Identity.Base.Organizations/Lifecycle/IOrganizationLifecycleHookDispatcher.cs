using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organizations.Lifecycle;

public interface IOrganizationLifecycleHookDispatcher
{
    Task EnsureCanCreateInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);
}
