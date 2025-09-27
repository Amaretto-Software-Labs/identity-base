using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Identity;
using Identity.Base.Roles.Options;
using Microsoft.Extensions.Options;

namespace Identity.Base.Roles.Services;

public sealed class DefaultUserRoleAssignmentListener : IUserCreationListener
{
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly RoleConfigurationOptions _roleOptions;

    public DefaultUserRoleAssignmentListener(
        IRoleAssignmentService roleAssignmentService,
        IOptions<RoleConfigurationOptions> roleOptions)
    {
        _roleAssignmentService = roleAssignmentService;
        _roleOptions = roleOptions.Value;
    }

    public async Task OnUserCreatedAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (_roleOptions.DefaultUserRoles is null || _roleOptions.DefaultUserRoles.Count == 0)
        {
            return;
        }

        var distinctRoles = _roleOptions.DefaultUserRoles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctRoles.Count == 0)
        {
            return;
        }

        await _roleAssignmentService.AssignRolesAsync(user.Id, distinctRoles, cancellationToken).ConfigureAwait(false);
    }
}
