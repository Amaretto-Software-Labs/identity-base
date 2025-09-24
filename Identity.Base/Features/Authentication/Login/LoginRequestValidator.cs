using FluentValidation;

namespace Identity.Base.Features.Authentication.Login;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12);

        RuleFor(x => x.ClientId)
            .NotEmpty();
    }
}
