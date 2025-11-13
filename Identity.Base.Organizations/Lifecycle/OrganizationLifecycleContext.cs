using System;
using System.Collections.Generic;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Lifecycle;

public enum OrganizationLifecycleEvent
{
    OrganizationCreated,
    OrganizationUpdated,
    OrganizationArchived,
    OrganizationRestored,
    InvitationCreated,
    InvitationRevoked,
    InvitationAccepted,
    MemberAdded,
    MembershipUpdated,
    MembershipRevoked
}

public sealed record OrganizationLifecycleContext(
    OrganizationLifecycleEvent Event,
    Guid OrganizationId,
    string? OrganizationSlug,
    string? OrganizationName,
    Guid? ActorUserId = null,
    Guid? TargetUserId = null,
    Organization? Organization = null,
    OrganizationInvitationRecord? Invitation = null,
    IReadOnlyDictionary<string, object?>? Items = null);
