using System;
using FluentValidation;
using Identity.Base.Organisations.Api.Models;

namespace Identity.Base.Organisations.Api.Validation;

public sealed class SetActiveOrganisationRequestValidator : AbstractValidator<SetActiveOrganisationRequest>
{
    public SetActiveOrganisationRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.OrganisationId != Guid.Empty || !string.IsNullOrWhiteSpace(request.OrganisationSlug))
            .WithMessage("Organisation identifier or slug is required.");
    }
}
