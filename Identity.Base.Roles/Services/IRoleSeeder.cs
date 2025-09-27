using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Roles.Services;

public interface IRoleSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
