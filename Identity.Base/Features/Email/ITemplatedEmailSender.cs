namespace Identity.Base.Features.Email;

public interface ITemplatedEmailSender
{
    Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default);
}

public sealed record TemplatedEmail(
    string TemplateKey,
    string ToEmail,
    string ToName,
    IDictionary<string, object?> Variables,
    string? Subject = null);
