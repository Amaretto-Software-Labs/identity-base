using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;

namespace Identity.Base.Abstractions;

public interface IUserCreationListener
{
    Task OnUserCreatedAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
