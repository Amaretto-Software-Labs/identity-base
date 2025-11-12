using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Lifecycle;

public interface IUserLifecycleHookDispatcher
{
    Task EnsureCanRegisterAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserRegisteredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanConfirmEmailAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserEmailConfirmedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanRequestEmailConfirmationAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyEmailConfirmationRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanResetPasswordAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanRequestPasswordResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyPasswordResetRequestedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanChangePasswordAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserPasswordChangedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanUpdateProfileAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserProfileUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanLockUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserLockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanUnlockUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserUnlockedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanUpdateRolesAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserRolesUpdatedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanEnableMfaAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserMfaEnabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanDisableMfaAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserMfaDisabledAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanResetMfaAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserMfaResetAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanGenerateRecoveryCodesAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyRecoveryCodesGeneratedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanDeleteUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserDeletedAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task EnsureCanRestoreUserAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);

    Task NotifyUserRestoredAsync(UserLifecycleContext context, CancellationToken cancellationToken = default);
}
