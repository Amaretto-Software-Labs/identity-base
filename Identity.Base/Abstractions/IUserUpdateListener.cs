using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;

namespace Identity.Base.Abstractions;

public interface IUserUpdateListener
{
    Task OnUserUpdatedAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
