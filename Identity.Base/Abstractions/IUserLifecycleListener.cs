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

    ValueTask<LifecycleHookResult> BeforeUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(LifecycleHookResult.Continue());

    ValueTask AfterUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
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
