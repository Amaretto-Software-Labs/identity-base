using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Lifecycle;

public interface IUserLifecycleHookDispatcher
{
    Task EnsureCanRegisterAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanConfirmEmailAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserEmailConfirmedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanUpdateProfileAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanDeleteUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserDeletedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanRestoreUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserRestoredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);
}
