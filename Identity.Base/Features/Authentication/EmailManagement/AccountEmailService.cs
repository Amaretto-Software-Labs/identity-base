using System.Text;
using Identity.Base.Features.Email;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Features.Authentication.EmailManagement;

public interface IAccountEmailService
{
    Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

public sealed class AccountEmailService : IAccountEmailService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITemplatedEmailSender _emailSender;
    private readonly RegistrationOptions _registrationOptions;
    private readonly MailJetOptions _mailJetOptions;
    private readonly ILogger<AccountEmailService> _logger;
    private readonly ILogSanitizer _sanitizer;

    public AccountEmailService(
        UserManager<ApplicationUser> userManager,
        ITemplatedEmailSender emailSender,
        IOptions<RegistrationOptions> registrationOptions,
        IOptions<MailJetOptions> mailJetOptions,
        ILogger<AccountEmailService> logger,
        ILogSanitizer sanitizer)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _registrationOptions = registrationOptions.Value;
        _mailJetOptions = mailJetOptions.Value;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (_mailJetOptions.Templates.Confirmation <= 0)
        {
            throw new InvalidOperationException("Confirmation template is not configured.");
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Encode(token);
        var confirmationUrl = BuildUrl(_registrationOptions.ConfirmationUrlTemplate, encodedToken, user.Email!);

        var variables = new Dictionary<string, object?>
        {
            ["email"] = user.Email,
            ["displayName"] = user.DisplayName ?? user.Email,
            ["confirmationUrl"] = confirmationUrl
        };

        var email = new TemplatedEmail(
            user.Email!,
            user.DisplayName ?? user.Email!,
            _mailJetOptions.Templates.Confirmation,
            variables,
            "Confirm your Identity Base account");

        await SendAsync(email, user.Email!, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (_mailJetOptions.Templates.PasswordReset <= 0)
        {
            throw new InvalidOperationException("Password reset template is not configured.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = Encode(token);
        var resetUrl = BuildUrl(_registrationOptions.PasswordResetUrlTemplate, encodedToken, user.Email!);

        var variables = new Dictionary<string, object?>
        {
            ["email"] = user.Email,
            ["displayName"] = user.DisplayName ?? user.Email,
            ["resetUrl"] = resetUrl
        };

        var email = new TemplatedEmail(
            user.Email!,
            user.DisplayName ?? user.Email!,
            _mailJetOptions.Templates.PasswordReset,
            variables,
            "Reset your Identity Base password");

        await SendAsync(email, user.Email!, cancellationToken);
    }

    private async Task SendAsync(TemplatedEmail email, string recipient, CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendAsync(email, cancellationToken);
            _logger.LogInformation("Dispatched email template {TemplateId} to {Recipient}", email.TemplateId, _sanitizer.RedactEmail(recipient));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Failed to send email template {TemplateId} to {Recipient}", email.TemplateId, _sanitizer.RedactEmail(recipient));
            throw;
        }
    }

    private static string Encode(string value)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));

    private static string BuildUrl(string template, string token, string email)
        => template
            .Replace("{token}", token, StringComparison.Ordinal)
            .Replace("{email}", WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(email)), StringComparison.Ordinal);
}
