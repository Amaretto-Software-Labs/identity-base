using FluentValidation;

namespace OrgSampleApi.Sample.Invitations;

public sealed class InvitationRegistrationRequestValidator : AbstractValidator<InvitationRegistrationRequest>
{
    public InvitationRegistrationRequestValidator()
    {
        RuleFor(x => x.InvitationCode)
            .NotEmpty();

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12);

        RuleFor(x => x.Metadata)
            .Must(metadata => metadata is not null)
            .WithMessage("Metadata payload must be provided (use an empty object if no fields supplied).");
    }
}
