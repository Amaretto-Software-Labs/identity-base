using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Organizations.Lifecycle;

public sealed class NullOrganizationLifecycleDispatcher : IOrganizationLifecycleHookDispatcher
{
    public static IOrganizationLifecycleHookDispatcher Instance { get; } = new NullOrganizationLifecycleDispatcher();

    private NullOrganizationLifecycleDispatcher()
    {
    }

    public Task EnsureCanCreateInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
