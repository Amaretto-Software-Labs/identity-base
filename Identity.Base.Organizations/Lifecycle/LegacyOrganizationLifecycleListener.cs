using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Lifecycle;
using Identity.Base.Organizations.Abstractions;

namespace Identity.Base.Organizations.Lifecycle;

internal sealed class LegacyOrganizationLifecycleListener : IOrganizationLifecycleListener
{
    private readonly IEnumerable<IOrganizationCreationListener> _creationListeners;
    private readonly IEnumerable<IOrganizationUpdateListener> _updateListeners;
    private readonly IEnumerable<IOrganizationArchiveListener> _archiveListeners;

    public LegacyOrganizationLifecycleListener(
        IEnumerable<IOrganizationCreationListener> creationListeners,
        IEnumerable<IOrganizationUpdateListener> updateListeners,
        IEnumerable<IOrganizationArchiveListener> archiveListeners)
    {
        _creationListeners = creationListeners;
        _updateListeners = updateListeners;
        _archiveListeners = archiveListeners;
    }

    public async ValueTask AfterOrganizationCreatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
    {
        foreach (var listener in _creationListeners)
        {
            if (context.Organization is { } organization)
            {
                await listener.OnOrganizationCreatedAsync(organization, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask AfterOrganizationUpdatedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
    {
        foreach (var listener in _updateListeners)
        {
            if (context.Organization is { } organization)
            {
                await listener.OnOrganizationUpdatedAsync(organization, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask AfterOrganizationArchivedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
    {
        foreach (var listener in _archiveListeners)
        {
            if (context.Organization is { } organization)
            {
                await listener.OnOrganizationArchivedAsync(organization, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public ValueTask AfterOrganizationRestoredAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask AfterInvitationAcceptedAsync(OrganizationLifecycleContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
