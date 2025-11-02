using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

public sealed class CreateOrganisationRequestValidator : AbstractValidator<CreateOrganisationRequest>
{
    public CreateOrganisationRequestValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9][a-z0-9-_.]*$");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(256);
    }
}
