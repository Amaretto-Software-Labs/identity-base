using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Base.OpenIddict;

public class OpenIddictToken : OpenIddictEntityFrameworkCoreToken<Guid, OpenIddictApplication, OpenIddictAuthorization>
{
}
