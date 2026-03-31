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
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128);
    }
}
