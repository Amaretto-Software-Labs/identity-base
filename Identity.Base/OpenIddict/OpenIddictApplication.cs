using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Base.OpenIddict;

public class OpenIddictApplication : OpenIddictEntityFrameworkCoreApplication<Guid, OpenIddictAuthorization, OpenIddictToken>
{
}
