using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

internal sealed class CreateOrganisationInvitationRequestValidator : AbstractValidator<CreateOrganisationInvitationRequest>
{
    public CreateOrganisationInvitationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.RoleIds)
            .Must(roleIds => roleIds is not null)
            .WithMessage("Role list must be provided (use an empty array if no roles should be assigned).");

        RuleFor(x => x.ExpiresInHours)
            .GreaterThanOrEqualTo(1)
            .When(x => x.ExpiresInHours.HasValue)
            .WithMessage("Expiration must be at least one hour.");
    }
}
