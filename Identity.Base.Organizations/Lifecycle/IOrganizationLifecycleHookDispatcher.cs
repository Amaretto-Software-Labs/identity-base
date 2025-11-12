using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organizations.Lifecycle;

public interface IOrganizationLifecycleHookDispatcher
{
    Task EnsureCanCreateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyOrganizationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanUpdateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyOrganizationUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanArchiveOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyOrganizationArchivedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanRestoreOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyOrganizationRestoredAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanCreateInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanRevokeInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanAddMemberAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanAcceptInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyInvitationAcceptedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanUpdateMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyMembershipUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanRevokeMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default);
}
