using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Domain;

namespace Identity.Base.Organizations.Abstractions;

public interface IOrganizationCreationListener
{
    Task OnOrganizationCreatedAsync(Organization organization, CancellationToken cancellationToken = default);
}
