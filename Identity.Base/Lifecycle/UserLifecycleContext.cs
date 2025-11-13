using System;
using System.Collections.Generic;
using Identity.Base.Identity;

namespace Identity.Base.Lifecycle;

public enum UserLifecycleEvent
{
    Registration,
    EmailConfirmation,
    EmailConfirmationRequested,
    PasswordReset,
    PasswordResetRequested,
    PasswordChanged,
    ProfileUpdated,
    Locked,
    Unlocked,
    RolesUpdated,
    MfaEnabled,
    MfaDisabled,
    MfaReset,
    MfaRecoveryCodesGenerated,
    Deleted,
    Restored
}

public sealed record UserLifecycleContext(
    UserLifecycleEvent Event,
    ApplicationUser User,
    Guid? ActorUserId = null,
    string? CorrelationId = null,
    string? Source = null,
    IReadOnlyDictionary<string, object?>? Items = null);
