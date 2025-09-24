using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Identity.Base.Identity;

public sealed class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    private readonly IOptions<RegistrationOptions> _registrationOptions;

    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor,
        IOptions<RegistrationOptions> registrationOptions)
        : base(userManager, roleManager, optionsAccessor)
    {
        _registrationOptions = registrationOptions;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        foreach (var field in _registrationOptions.Value.ProfileFields)
        {
            if (user.ProfileMetadata.Values.TryGetValue(field.Name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                identity.AddClaim(new Claim($"profile:{field.Name}", value));
            }
        }

        return identity;
    }
}
