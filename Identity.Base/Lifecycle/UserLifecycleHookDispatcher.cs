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

    public Task EnsureCanUpdateProfileAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeUserProfileUpdatedAsync(ctx, token), nameof(IUserLifecycleListener.BeforeUserProfileUpdatedAsync), cancellationToken);

    public Task NotifyUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterUserProfileUpdatedAsync(ctx, token), nameof(IUserLifecycleListener.AfterUserProfileUpdatedAsync), cancellationToken);

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
}
