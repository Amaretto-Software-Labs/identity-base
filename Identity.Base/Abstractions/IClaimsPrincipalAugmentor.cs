using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Identity;

namespace Identity.Base.Abstractions;

public interface IClaimsPrincipalAugmentor
{
    Task AugmentAsync(ApplicationUser user, ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
