using FluentValidation;
using Identity.Base.Organizations.Api.Models;

namespace Identity.Base.Organizations.Api.Validation;

public sealed class UpdateMembershipRequestValidator : AbstractValidator<UpdateMembershipRequest>
{
    public UpdateMembershipRequestValidator()
    {
        RuleFor(x => x)
            .Must(request => request.IsPrimary.HasValue || request.RoleIds is not null)
            .WithMessage("At least one field must be provided.");

        When(x => x.RoleIds is not null, () =>
        {
            RuleFor(x => x.RoleIds!)
                .NotEmpty();
        });
    }
}
