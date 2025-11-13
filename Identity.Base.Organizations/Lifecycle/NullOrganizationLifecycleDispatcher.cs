using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organizations.Lifecycle;

public sealed class NullOrganizationLifecycleDispatcher : IOrganizationLifecycleHookDispatcher
{
    public static IOrganizationLifecycleHookDispatcher Instance { get; } = new NullOrganizationLifecycleDispatcher();

    private NullOrganizationLifecycleDispatcher()
    {
    }

    public Task EnsureCanCreateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyOrganizationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanUpdateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyOrganizationUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanArchiveOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyOrganizationArchivedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanRestoreOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyOrganizationRestoredAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanCreateInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanRevokeInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanAddMemberAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanAcceptInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyInvitationAcceptedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanUpdateMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyMembershipUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureCanRevokeMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
