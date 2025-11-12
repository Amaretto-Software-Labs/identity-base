using System;
using System.Collections.Generic;
using Identity.Base.Organizations.Abstractions;

namespace Identity.Base.Organizations.Lifecycle;

public enum OrganizationLifecycleEvent
{
    InvitationCreated,
    InvitationRevoked,
    MemberAdded,
    MembershipRevoked
}

public sealed record OrganizationLifecycleContext(
    OrganizationLifecycleEvent Event,
    Guid OrganizationId,
    string? OrganizationSlug,
    string? OrganizationName,
    Guid? ActorUserId = null,
    Guid? TargetUserId = null,
    OrganizationInvitationRecord? Invitation = null,
    IReadOnlyDictionary<string, object?>? Items = null);
