using System;
using FluentValidation;
using Identity.Base.Organizations.Api.Models;

namespace Identity.Base.Organizations.Api.Validation;

public sealed class SetActiveOrganizationRequestValidator : AbstractValidator<SetActiveOrganizationRequest>
{
    public SetActiveOrganizationRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.OrganizationId != Guid.Empty || !string.IsNullOrWhiteSpace(request.OrganizationSlug))
            .WithMessage("Organization identifier or slug is required.");
    }
}
