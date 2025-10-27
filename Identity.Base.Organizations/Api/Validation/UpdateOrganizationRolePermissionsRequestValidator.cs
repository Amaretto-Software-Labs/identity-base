using FluentValidation;
using Identity.Base.Organizations.Api.Models;

namespace Identity.Base.Organizations.Api.Validation;

public sealed class UpdateOrganizationRolePermissionsRequestValidator : AbstractValidator<UpdateOrganizationRolePermissionsRequest>
{
    public UpdateOrganizationRolePermissionsRequestValidator()
    {
        RuleFor(request => request.Permissions)
            .NotNull();

        RuleForEach(request => request.Permissions)
            .NotEmpty()
            .MaximumLength(128);
    }
}
