using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Lifecycle;
using Identity.Base.Organizations.Lifecycle;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationLifecycleListener
{
    ValueTask<LifecycleHookResult> BeforeOrganizationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterOrganizationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeOrganizationUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterOrganizationUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeOrganizationArchivedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterOrganizationArchivedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeOrganizationRestoredAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterOrganizationRestoredAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeInvitationAcceptedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterInvitationAcceptedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeMembershipUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterMembershipUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
