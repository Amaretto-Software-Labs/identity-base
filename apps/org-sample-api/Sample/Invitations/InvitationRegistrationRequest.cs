using System;
using System.Collections.Generic;

namespace OrgSampleApi.Sample.Invitations;

public sealed class InvitationRegistrationRequest
{
    public Guid InvitationCode { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public IDictionary<string, string?> Metadata { get; set; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
