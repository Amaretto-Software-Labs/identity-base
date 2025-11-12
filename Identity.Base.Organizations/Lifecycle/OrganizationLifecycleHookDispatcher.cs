using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Lifecycle;
using Identity.Base.Organizations.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Lifecycle;

internal sealed class OrganizationLifecycleHookDispatcher : IOrganizationLifecycleHookDispatcher
{
    private readonly IEnumerable<IOrganizationLifecycleListener> _listeners;
    private readonly ILogger<OrganizationLifecycleHookDispatcher> _logger;
    private readonly LifecycleHookOptions _options;

    public OrganizationLifecycleHookDispatcher(
        IEnumerable<IOrganizationLifecycleListener> listeners,
        ILogger<OrganizationLifecycleHookDispatcher> logger,
        IOptions<LifecycleHookOptions> options)
    {
        _listeners = listeners;
        _logger = logger;
        _options = options.Value;
    }

    public Task EnsureCanCreateInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeInvitationCreatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeInvitationCreatedAsync), cancellationToken);

    public Task NotifyInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterInvitationCreatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterInvitationCreatedAsync), cancellationToken);

    public Task NotifyInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterInvitationRevokedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterInvitationRevokedAsync), cancellationToken);

    public Task NotifyMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterMemberAddedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterMemberAddedAsync), cancellationToken);

    public Task NotifyMembershipRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterMembershipRevokedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterMembershipRevokedAsync), cancellationToken);

    private async Task ExecuteBeforeAsync(
        OrganizationLifecycleContext context,
        Func<IOrganizationLifecycleListener, OrganizationLifecycleContext, CancellationToken, ValueTask<LifecycleHookResult>> callback,
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
        OrganizationLifecycleContext context,
        Func<IOrganizationLifecycleListener, OrganizationLifecycleContext, CancellationToken, ValueTask> callback,
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
                    "Organization lifecycle after-hook '{Operation}' failed in {ListenerType}. Continuing execution.",
                    operation,
                    listener.GetType().FullName);
            }
        }
    }
}
