using System;

namespace Identity.Base.Features.Notifications;

public sealed class NotificationAugmentationException : InvalidOperationException
{
    public NotificationAugmentationException(string message)
        : base(message)
    {
    }

    public NotificationAugmentationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
