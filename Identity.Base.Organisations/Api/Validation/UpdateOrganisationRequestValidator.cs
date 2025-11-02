using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

public sealed class UpdateOrganisationRequestValidator : AbstractValidator<UpdateOrganisationRequest>
{
    public UpdateOrganisationRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(256);

        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue);
    }
}
