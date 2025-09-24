using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Base.OpenIddict;

public class OpenIddictAuthorization : OpenIddictEntityFrameworkCoreAuthorization<Guid, OpenIddictApplication, OpenIddictToken>
{
}
