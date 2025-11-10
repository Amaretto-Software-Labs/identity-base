using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;

namespace Identity.Base.Abstractions;

public interface IUserRestoreListener
{
    Task OnUserRestoredAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
