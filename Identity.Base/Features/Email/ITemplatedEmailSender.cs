namespace Identity.Base.Features.Email;

public interface ITemplatedEmailSender
{
    Task SendAsync(TemplatedEmail email, CancellationToken cancellationToken = default);
}

public sealed record TemplatedEmail(
    string ToEmail,
    string ToName,
    long TemplateId,
    IDictionary<string, object?> Variables,
    string? Subject = null);
