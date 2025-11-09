using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationUpdateListener
{
    Task OnOrganizationUpdatedAsync(Organization organization, CancellationToken cancellationToken = default);
}
