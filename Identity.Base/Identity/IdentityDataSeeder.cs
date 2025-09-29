using Identity.Base.Abstractions;
using Identity.Base.Logging;
using Identity.Base.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Base.Identity;

internal sealed class IdentityDataSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IEnumerable<IUserCreationListener> _creationListeners;
    private readonly IOptions<IdentitySeedOptions> _options;
    private readonly ILogger<IdentityDataSeeder> _logger;
    private readonly ILogSanitizer _sanitizer;
    private readonly IdentityBaseSeedCallbacks _seedCallbacks;
    private readonly IServiceProvider _serviceProvider;

    public IdentityDataSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IEnumerable<IUserCreationListener> creationListeners,
        IOptions<IdentitySeedOptions> options,
        ILogger<IdentityDataSeeder> logger,
        ILogSanitizer sanitizer,
        IdentityBaseSeedCallbacks seedCallbacks,
        IServiceProvider serviceProvider)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _creationListeners = creationListeners;
        _options = options;
        _logger = logger;
        _sanitizer = sanitizer;
        _seedCallbacks = seedCallbacks;
        _serviceProvider = serviceProvider;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogDebug("Identity seeding disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            _logger.LogWarning("Identity seeding enabled but Email or Password not provided.");
            await ExecuteCallbacksAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var roleName in options.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var role = new ApplicationRole(roleName);
            var roleResult = await _roleManager.CreateAsync(role);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Failed to create seed role {Role}: {Errors}", roleName, string.Join(",", roleResult.Errors.Select(e => e.Description)));
            }
        }

        var existingUser = await _userManager.FindByEmailAsync(options.Email);
        if (existingUser is not null)
        {
            _logger.LogInformation("Seed user {Email} already exists.", _sanitizer.RedactEmail(options.Email));
            await ExecuteCallbacksAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = options.Email,
            Email = options.Email,
            EmailConfirmed = true,
            DisplayName = "Seed Administrator"
        };

        var createUserResult = await _userManager.CreateAsync(user, options.Password);
        if (!createUserResult.Succeeded)
        {
            _logger.LogWarning("Failed to create seed user {Email}: {Errors}", _sanitizer.RedactEmail(options.Email), string.Join(",", createUserResult.Errors.Select(e => e.Description)));
            await ExecuteCallbacksAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (options.Roles.Length > 0)
        {
            var addToRoleResult = await _userManager.AddToRolesAsync(user, options.Roles);
            if (!addToRoleResult.Succeeded)
            {
                _logger.LogWarning("Failed to add seed user {Email} to roles: {Errors}", _sanitizer.RedactEmail(options.Email), string.Join(",", addToRoleResult.Errors.Select(e => e.Description)));
                await ExecuteCallbacksAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        foreach (var listener in _creationListeners)
        {
            await listener.OnUserCreatedAsync(user, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Seed user {Email} created successfully.", _sanitizer.RedactEmail(options.Email));

        await ExecuteCallbacksAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteCallbacksAsync(CancellationToken cancellationToken)
    {
        foreach (var callback in _seedCallbacks.IdentitySeedCallbacks)
        {
            await callback(_serviceProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
