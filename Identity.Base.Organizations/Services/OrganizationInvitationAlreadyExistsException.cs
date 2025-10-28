using System;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationInvitationAlreadyExistsException : InvalidOperationException
{
    public OrganizationInvitationAlreadyExistsException(string email)
        : base($"An active invitation already exists for {email}.")
    {
        Email = email;
    }

    public string Email { get; }
}
