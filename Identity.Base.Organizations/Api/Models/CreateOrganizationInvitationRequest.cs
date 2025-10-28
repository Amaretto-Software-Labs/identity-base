using System;
using System.Collections.Generic;

namespace Identity.Base.Organizations.Api.Models;

public sealed class CreateOrganizationInvitationRequest
{
    public string Email { get; set; } = string.Empty;

    public IReadOnlyCollection<Guid> RoleIds { get; set; } = Array.Empty<Guid>();

    public int? ExpiresInHours { get; set; }
}
