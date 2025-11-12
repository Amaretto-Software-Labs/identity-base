using System.Diagnostics.CodeAnalysis;

namespace Identity.Base.Features.Notifications;

public readonly record struct NotificationAugmentorResult
{
    private NotificationAugmentorResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        ErrorMessage = error;
    }

    public bool Succeeded { get; }

    public string? ErrorMessage { get; }

    public static NotificationAugmentorResult Continue() => new(true, null);

    public static NotificationAugmentorResult Fail(string? error = null) => new(false, error);

    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsFailure => !Succeeded;
}
