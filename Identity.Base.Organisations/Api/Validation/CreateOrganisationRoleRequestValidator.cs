using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

public sealed class CreateOrganisationRoleRequestValidator : AbstractValidator<CreateOrganisationRoleRequest>
{
    public CreateOrganisationRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(512);
    }
}
