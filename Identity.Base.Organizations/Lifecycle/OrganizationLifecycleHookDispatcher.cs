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

    public Task EnsureCanCreateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeOrganizationCreatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeOrganizationCreatedAsync), cancellationToken);

    public Task NotifyOrganizationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterOrganizationCreatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterOrganizationCreatedAsync), cancellationToken);

    public Task EnsureCanUpdateOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeOrganizationUpdatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeOrganizationUpdatedAsync), cancellationToken);

    public Task NotifyOrganizationUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterOrganizationUpdatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterOrganizationUpdatedAsync), cancellationToken);

    public Task EnsureCanArchiveOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeOrganizationArchivedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeOrganizationArchivedAsync), cancellationToken);

    public Task NotifyOrganizationArchivedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterOrganizationArchivedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterOrganizationArchivedAsync), cancellationToken);

    public Task EnsureCanRestoreOrganizationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeOrganizationRestoredAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeOrganizationRestoredAsync), cancellationToken);

    public Task NotifyOrganizationRestoredAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterOrganizationRestoredAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterOrganizationRestoredAsync), cancellationToken);

    public Task EnsureCanCreateInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeInvitationCreatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeInvitationCreatedAsync), cancellationToken);

    public Task NotifyInvitationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterInvitationCreatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterInvitationCreatedAsync), cancellationToken);

    public Task EnsureCanRevokeInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeInvitationRevokedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeInvitationRevokedAsync), cancellationToken);

    public Task NotifyInvitationRevokedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterInvitationRevokedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterInvitationRevokedAsync), cancellationToken);

    public Task EnsureCanAddMemberAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeMemberAddedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeMemberAddedAsync), cancellationToken);

    public Task NotifyMemberAddedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterMemberAddedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterMemberAddedAsync), cancellationToken);

    public Task EnsureCanAcceptInvitationAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeInvitationAcceptedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeInvitationAcceptedAsync), cancellationToken);

    public Task NotifyInvitationAcceptedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterInvitationAcceptedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterInvitationAcceptedAsync), cancellationToken);

    public Task EnsureCanUpdateMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeMembershipUpdatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeMembershipUpdatedAsync), cancellationToken);

    public Task NotifyMembershipUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteAfterAsync(context, static (listener, ctx, token) => listener.AfterMembershipUpdatedAsync(ctx, token), nameof(IOrganizationLifecycleListener.AfterMembershipUpdatedAsync), cancellationToken);

    public Task EnsureCanRevokeMembershipAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ExecuteBeforeAsync(context, static (listener, ctx, token) => listener.BeforeMembershipRevokedAsync(ctx, token), nameof(IOrganizationLifecycleListener.BeforeMembershipRevokedAsync), cancellationToken);

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
                    "Organization lifecycle after-hook '{Operation}' failed in {ListenerType}. Continuing execution.",
                    operation,
                    listener.GetType().FullName);
            }
        }
    }

    private static bool IsCriticalException(Exception exception)
        => exception is OutOfMemoryException or StackOverflowException or ThreadAbortException;
}
