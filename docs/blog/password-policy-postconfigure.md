# Password Policy and Validators with PostConfigure

As of 0.7.16, Identity Base fully defers to ASP.NET Core Identity for password rules. That means your host owns the policy and can tune it using the same `IdentityOptions` you already use elsewhere in ASP.NET Core.

## Objective

Make password complexity predictable and centralized. By configuring `IdentityOptions.Password` and adding custom validators, you get one source of truth that applies to registration, password reset, and any admin-driven password changes.

## Usage

Use `PostConfigure<IdentityOptions>` to set your baseline policy, then add custom validators when you need rules beyond the built-in complexity settings. `PostConfigure` is useful because it applies after all other configuration, so your host values win even if defaults are registered elsewhere. The examples below show a strict policy, a custom validator, and an environment-specific override.

## Example: Configure password complexity

```csharp
using Microsoft.AspNetCore.Identity;

builder.Services.PostConfigure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 14;
    options.Password.RequiredUniqueChars = 4;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
});
```

## Example: Add a custom password validator

Custom validators run in addition to the built-in checks, which makes them ideal for product-specific rules (for example, banning leaked passwords or preventing personal data reuse).

```csharp
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;

public sealed class NoPersonalInfoPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string password)
    {
        if (!string.IsNullOrWhiteSpace(user.Email) &&
            password.Contains(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordContainsEmail",
                Description = "Password must not contain the email address."
            }));
        }

        return Task.FromResult(IdentityResult.Success);
    }
}

builder.Services.AddScoped<IPasswordValidator<ApplicationUser>, NoPersonalInfoPasswordValidator>();
```

## Example: Environment-specific policy

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.PostConfigure<IdentityOptions>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
    });
}
```

## Migration tips

If you previously configured password rules through a custom Identity Base section, remove that configuration. The effective policy is now entirely defined by ASP.NET Core Identity options and validators, so ensure your `IdentityOptions.Password` values reflect your actual requirements.
