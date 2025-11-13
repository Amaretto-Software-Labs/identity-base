using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Features.Notifications;

public interface INotificationContextPipeline<TContext>
    where TContext : NotificationContext
{
    Task RunAsync(TContext context, CancellationToken cancellationToken = default);
}
