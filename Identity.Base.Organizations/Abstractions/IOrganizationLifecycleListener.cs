using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Lifecycle;
using Identity.Base.Organizations.Lifecycle;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationLifecycleListener
{
    ValueTask<LifecycleHookResult> BeforeInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask AfterInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask AfterMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask AfterMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
