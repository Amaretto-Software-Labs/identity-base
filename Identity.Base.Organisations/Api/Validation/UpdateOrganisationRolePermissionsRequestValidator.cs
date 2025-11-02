using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

public sealed class UpdateOrganisationRolePermissionsRequestValidator : AbstractValidator<UpdateOrganisationRolePermissionsRequest>
{
    public UpdateOrganisationRolePermissionsRequestValidator()
    {
        RuleFor(request => request.Permissions)
            .NotNull();

        RuleForEach(request => request.Permissions)
            .NotEmpty()
            .MaximumLength(128);
    }
}
