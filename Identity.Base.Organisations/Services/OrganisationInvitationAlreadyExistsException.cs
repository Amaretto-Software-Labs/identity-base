using System;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationInvitationAlreadyExistsException : InvalidOperationException
{
    public OrganisationInvitationAlreadyExistsException(string email)
        : base($"An active invitation already exists for {email}.")
    {
        Email = email;
    }

    public string Email { get; }
}
