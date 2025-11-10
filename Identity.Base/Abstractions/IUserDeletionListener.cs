using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;

namespace Identity.Base.Abstractions;

public interface IUserDeletionListener
{
    Task OnUserDeletedAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
