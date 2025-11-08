using FluentValidation;
using Identity.Base.Organizations.Api.Models;

namespace Identity.Base.Organizations.Api.Validation;

public sealed class UpdateMembershipRequestValidator : AbstractValidator<UpdateMembershipRequest>
{
    public UpdateMembershipRequestValidator()
    {
    }
}
