using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Lifecycle;

internal sealed class UserLifecycleHookDispatcher : IUserLifecycleHookDispatcher
{
    private readonly IEnumerable<IUserLifecycleListener> _listeners;
    private readonly ILogger<UserLifecycleHookDispatcher> _logger;
    private readonly LifecycleHookOptions _options;

    public UserLifecycleHookDispatcher(
        IEnumerable<IUserLifecycleListener> listeners,
        ILogger<UserLifecycleHookDispatcher> logger,
        IOptions<LifecycleHookOptions> options)
    {
        _listeners = listeners;
        _logger = logger;
        _options = options.Value;
    }

    public Task EnsureCanRegisterAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserRegisteredAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserRegisteredAsync), cancellationToken);

    public Task NotifyUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserRegisteredAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserRegisteredAsync), cancellationToken);

    public Task EnsureCanConfirmEmailAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserEmailConfirmedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserEmailConfirmedAsync), cancellationToken);

    public Task NotifyUserEmailConfirmedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserEmailConfirmedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserEmailConfirmedAsync), cancellationToken);

    public Task EnsureCanRequestEmailConfirmationAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeEmailConfirmationRequestedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeEmailConfirmationRequestedAsync), cancellationToken);

    public Task NotifyEmailConfirmationRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterEmailConfirmationRequestedAsync(ctx, token), nameof(IUserLifecycleListener.AfterEmailConfirmationRequestedAsync), cancellationToken);

    public Task EnsureCanResetPasswordAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserPasswordResetAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserPasswordResetAsync), cancellationToken);

    public Task NotifyUserPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserPasswordResetAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserPasswordResetAsync), cancellationToken);

    public Task EnsureCanRequestPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforePasswordResetRequestedAsync(ctx, token), nameof(IUserLifecycleListener.BeforePasswordResetRequestedAsync), cancellationToken);

    public Task NotifyPasswordResetRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterPasswordResetRequestedAsync(ctx, token), nameof(IUserLifecycleListener.AfterPasswordResetRequestedAsync), cancellationToken);

    public Task EnsureCanChangePasswordAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserPasswordChangedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserPasswordChangedAsync), cancellationToken);

    public Task NotifyUserPasswordChangedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserPasswordChangedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserPasswordChangedAsync), cancellationToken);

    public Task EnsureCanUpdateProfileAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserProfileUpdatedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserProfileUpdatedAsync), cancellationToken);

    public Task NotifyUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserProfileUpdatedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserProfileUpdatedAsync), cancellationToken);

    public Task EnsureCanLockUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserLockedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserLockedAsync), cancellationToken);

    public Task NotifyUserLockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserLockedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserLockedAsync), cancellationToken);

    public Task EnsureCanUnlockUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserUnlockedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserUnlockedAsync), cancellationToken);

    public Task NotifyUserUnlockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserUnlockedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserUnlockedAsync), cancellationToken);

    public Task EnsureCanUpdateRolesAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserRolesUpdatedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserRolesUpdatedAsync), cancellationToken);

    public Task NotifyUserRolesUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserRolesUpdatedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserRolesUpdatedAsync), cancellationToken);

    public Task EnsureCanEnableMfaAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserMfaEnabledAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserMfaEnabledAsync), cancellationToken);

    public Task NotifyUserMfaEnabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserMfaEnabledAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserMfaEnabledAsync), cancellationToken);

    public Task EnsureCanDisableMfaAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserMfaDisabledAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserMfaDisabledAsync), cancellationToken);

    public Task NotifyUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserMfaDisabledAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserMfaDisabledAsync), cancellationToken);

    public Task EnsureCanResetMfaAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserMfaResetAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserMfaResetAsync), cancellationToken);

    public Task NotifyUserMfaResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserMfaResetAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserMfaResetAsync), cancellationToken);

    public Task EnsureCanGenerateRecoveryCodesAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeRecoveryCodesGeneratedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeRecoveryCodesGeneratedAsync), cancellationToken);

    public Task NotifyRecoveryCodesGeneratedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterRecoveryCodesGeneratedAsync(ctx, token), nameof(IUserLifecycleListener.AfterRecoveryCodesGeneratedAsync), cancellationToken);

    public Task EnsureCanDeleteUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserDeletedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserDeletedAsync), cancellationToken);

    public Task NotifyUserDeletedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserDeletedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserDeletedAsync), cancellationToken);

    public Task EnsureCanRestoreUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserRestoredAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserRestoredAsync), cancellationToken);

    public Task NotifyUserRestoredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserRestoredAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserRestoredAsync), cancellationToken);

    private async Task ExecuteBeforeAsync(
        UserLifecycleContext context,
        Func<IUserLifecycleListener, UserLifecycleContext, CancellationToken, ValueTask<LifecycleHookResult>> callback,
        string operation,
        CancellationToken cancellationToken)
    {
        foreach (var listener in _listeners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LifecycleHookResult result;
            try
            {
                result = await callback(listener, context, cancellationToken).ConfigureAwait(false);
            }
            catch (LifecycleHookRejectedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (IsCriticalException(exception))
                {
                    throw;
                }

                throw new LifecycleHookExecutionException(operation, exception);
            }

            if (result.IsFailure)
            {
                throw new LifecycleHookRejectedException(operation, result.ErrorMessage);
            }
        }
    }

    private async Task ExecuteAfterAsync(
        UserLifecycleContext context,
        Func<IUserLifecycleListener, UserLifecycleContext, CancellationToken, ValueTask> callback,
        string operation,
        CancellationToken cancellationToken)
    {
        foreach (var listener in _listeners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await callback(listener, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (IsCriticalException(exception))
                {
                    throw;
                }

                if (_options.AfterFailureBehavior == LifecycleHookFailureBehavior.Bubble)
                {
                    throw new LifecycleHookExecutionException(operation, exception);
                }

                _logger.LogError(
                    exception,
                    "User lifecycle after-hook '{Operation}' failed in {ListenerType}. Continuing execution.",
                    operation,
                    listener.GetType().FullName);
            }
        }
    }

    private static bool IsCriticalException(Exception exception)
        => exception is OutOfMemoryException or StackOverflowException or ThreadAbortException;
}
