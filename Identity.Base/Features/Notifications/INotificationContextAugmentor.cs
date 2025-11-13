using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Features.Notifications;

public interface INotificationContextAugmentor<in TContext>
    where TContext : NotificationContext
{
    ValueTask<NotificationAugmentorResult> AugmentAsync(TContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(NotificationAugmentorResult.Continue());
}
