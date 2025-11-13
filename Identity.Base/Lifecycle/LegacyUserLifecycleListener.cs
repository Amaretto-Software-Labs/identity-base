using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;

namespace Identity.Base.Lifecycle;

internal sealed class LegacyUserLifecycleListener : IUserLifecycleListener
{
    private readonly IEnumerable<IUserCreationListener> _creationListeners;
    private readonly IEnumerable<IUserUpdateListener> _updateListeners;
    private readonly IEnumerable<IUserDeletionListener> _deletionListeners;
    private readonly IEnumerable<IUserRestoreListener> _restoreListeners;

    public LegacyUserLifecycleListener(
        IEnumerable<IUserCreationListener> creationListeners,
        IEnumerable<IUserUpdateListener> updateListeners,
        IEnumerable<IUserDeletionListener> deletionListeners,
        IEnumerable<IUserRestoreListener> restoreListeners)
    {
        _creationListeners = creationListeners;
        _updateListeners = updateListeners;
        _deletionListeners = deletionListeners;
        _restoreListeners = restoreListeners;
    }

    public async ValueTask AfterUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
    {
        foreach (var listener in _creationListeners)
        {
            await listener.OnUserCreatedAsync(context.User, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask AfterUserEmailConfirmedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterEmailConfirmationRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterPasswordResetRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterUserLockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterUserUnlockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterUserMfaEnabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterRecoveryCodesGeneratedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public async ValueTask AfterUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
    {
        foreach (var listener in _updateListeners)
        {
            await listener.OnUserUpdatedAsync(context.User, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask AfterUserDeletedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
    {
        foreach (var listener in _deletionListeners)
        {
            await listener.OnUserDeletedAsync(context.User, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask AfterUserRestoredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
    {
        foreach (var listener in _restoreListeners)
        {
            await listener.OnUserRestoredAsync(context.User, cancellationToken).ConfigureAwait(false);
        }
    }
}
