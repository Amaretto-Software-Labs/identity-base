using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Identity.Base.Features.Notifications;

internal sealed class NotificationContextPipeline<TContext> : INotificationContextPipeline<TContext>
    where TContext : NotificationContext
{
    private readonly IEnumerable<INotificationContextAugmentor<TContext>> _augmentors;
    private readonly ILogger<NotificationContextPipeline<TContext>> _logger;

    public NotificationContextPipeline(
        IEnumerable<INotificationContextAugmentor<TContext>> augmentors,
        ILogger<NotificationContextPipeline<TContext>> logger)
    {
        _augmentors = augmentors;
        _logger = logger;
    }

    public async Task RunAsync(TContext context, CancellationToken cancellationToken = default)
    {
        foreach (var augmentor in _augmentors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NotificationAugmentorResult result;
            try
            {
                result = await augmentor.AugmentAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (IsCriticalException(exception))
                {
                    throw;
                }

                _logger.LogError(
                    exception,
                    "Notification augmentor {Augmentor} failed for template {TemplateKey}.",
                    augmentor.GetType().FullName,
                    context.TemplateKey);
                throw new NotificationAugmentationException($"Notification augmentor '{augmentor.GetType().FullName}' failed.", exception);
            }

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Notification augmentor {Augmentor} rejected template {TemplateKey}: {Reason}",
                    augmentor.GetType().FullName,
                    context.TemplateKey,
                    result.ErrorMessage);
                throw new NotificationAugmentationException(result.ErrorMessage ?? "Notification augmentation rejected.");
            }
        }
    }

    private static bool IsCriticalException(Exception exception)
        => exception is OutOfMemoryException or StackOverflowException or ThreadAbortException;
}
