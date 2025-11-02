using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

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
