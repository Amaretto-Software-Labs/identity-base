using Microsoft.AspNetCore.Identity;

namespace Identity.Base.Identity;

public sealed class ApplicationUserPasskey : IdentityUserPasskey<Guid>
{
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}
