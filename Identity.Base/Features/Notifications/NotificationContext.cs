using System.Collections.Generic;
using Identity.Base.Features.Email;
using Identity.Base.Identity;

namespace Identity.Base.Features.Notifications;

public static class NotificationChannels
{
    public const string Email = "email";
    public const string Sms = "sms";
}

public abstract class NotificationContext
{
    protected NotificationContext(
        string templateKey,
        ApplicationUser user,
        string? subject = null,
        string channel = NotificationChannels.Email)
    {
        TemplateKey = templateKey;
        User = user;
        Subject = subject;
        Channel = channel;
        Variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        Metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public string TemplateKey { get; set; }

    public ApplicationUser User { get; }

    public string RecipientEmail => User.Email ?? string.Empty;

    public string RecipientName => User.DisplayName ?? User.Email ?? string.Empty;

    public string? Subject { get; set; }

    public string Channel { get; set; }

    public string? Locale { get; set; }

    public IDictionary<string, object?> Variables { get; }

    public IDictionary<string, object?> Metadata { get; }

    public TemplatedEmail ToTemplatedEmail()
        => new(TemplateKey, RecipientEmail, RecipientName, Variables, Subject);
}
