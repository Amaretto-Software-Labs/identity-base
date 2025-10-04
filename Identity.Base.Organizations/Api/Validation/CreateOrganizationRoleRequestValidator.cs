using FluentValidation;
using Identity.Base.Organizations.Api.Models;

namespace Identity.Base.Organizations.Api.Validation;

public sealed class CreateOrganizationRoleRequestValidator : AbstractValidator<CreateOrganizationRoleRequest>
{
    public CreateOrganizationRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(512);
    }
}
