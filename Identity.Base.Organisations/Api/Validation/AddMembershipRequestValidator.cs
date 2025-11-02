using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

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
