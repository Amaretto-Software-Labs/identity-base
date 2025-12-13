using FluentValidation;
using Identity.Base.Options;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.Register;

internal sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator(IOptions<RegistrationOptions> options)
    {
        var registration = options.Value;
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12);

        RuleFor(x => x.Metadata)
            .Must(metadata => metadata is not null)
            .WithMessage("Metadata payload must be provided (use an empty object if no fields supplied).");

        RuleFor(x => x)
            .Custom((request, context) => ValidateMetadata(request, registration, context));
    }

    private static void ValidateMetadata(RegisterUserRequest request, RegistrationOptions options, ValidationContext<RegisterUserRequest> context)
    {
        if (request.Metadata is null)
        {
            return;
        }

        var allowedFields = options.ProfileFields.ToDictionary(field => field.Name, field => field, StringComparer.OrdinalIgnoreCase);

        foreach (var key in request.Metadata.Keys)
        {
            if (!allowedFields.ContainsKey(key))
            {
                context.AddFailure(key, $"Metadata field '{key}' is not allowed.");
            }
        }

        foreach (var field in allowedFields.Values)
        {
            request.Metadata.TryGetValue(field.Name, out var value);

            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                context.AddFailure(field.Name, $"{field.DisplayName} is required.");
                continue;
            }

            if (!string.IsNullOrEmpty(value) && value.Length > field.MaxLength)
            {
                context.AddFailure(field.Name, $"{field.DisplayName} exceeds maximum length of {field.MaxLength} characters.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(field.Pattern) && !string.IsNullOrWhiteSpace(value))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                        value,
                        field.Pattern,
                        System.Text.RegularExpressions.RegexOptions.None,
                        TimeSpan.FromMilliseconds(250)))
                {
                    context.AddFailure(field.Name, $"{field.DisplayName} is not in the expected format.");
                }
            }
        }
    }
}
