using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Roles.Services;

public interface IRoleAssignmentService
{
    Task AssignRolesAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUserRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
