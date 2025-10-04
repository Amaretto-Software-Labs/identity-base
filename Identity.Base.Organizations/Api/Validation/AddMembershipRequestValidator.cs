using FluentValidation;
using Identity.Base.Organizations.Api.Models;

namespace Identity.Base.Organizations.Api.Validation;

public sealed class AddMembershipRequestValidator : AbstractValidator<AddMembershipRequest>
{
    public AddMembershipRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.RoleIds)
            .NotNull();
    }
}
