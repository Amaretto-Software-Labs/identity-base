using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Roles.Services;

public interface IPermissionResolver
{
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
