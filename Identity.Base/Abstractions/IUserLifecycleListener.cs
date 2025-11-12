using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Lifecycle;

namespace Identity.Base.Abstractions;

public interface IUserLifecycleListener
{
    ValueTask<LifecycleHookResult> BeforeUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserEmailConfirmedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserEmailConfirmedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeEmailConfirmationRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterEmailConfirmationRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforePasswordResetRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterPasswordResetRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserPasswordChangedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserPasswordChangedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserLockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserLockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserUnlockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserUnlockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserRolesUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserRolesUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserMfaEnabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserMfaEnabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserMfaResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserMfaResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeRecoveryCodesGeneratedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterRecoveryCodesGeneratedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserDeletedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserDeletedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    ValueTask<LifecycleHookResult> BeforeUserRestoredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserRestoredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
