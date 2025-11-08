using FluentValidation;
using Identity.Base.Organizations.Api.Models;

namespace Identity.Base.Organizations.Api.Validation;

public sealed class UpdateMembershipRequestValidator : AbstractValidator<UpdateMembershipRequest>
{
    public UpdateMembershipRequestValidator()
    {
        RuleFor(x => x.RoleIds)
            .NotNull()
            .Must(roleIds => roleIds?.Count > 0)
            .WithMessage("At least one role must be provided.");
    }
}
