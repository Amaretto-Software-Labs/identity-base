using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Roles.Services;

public sealed class CompositePermissionResolver : IPermissionResolver
{
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly IEnumerable<IAdditionalPermissionSource> _additionalSources;

    public CompositePermissionResolver(
        IRoleAssignmentService roleAssignmentService,
        IEnumerable<IAdditionalPermissionSource> additionalSources)
    {
        _roleAssignmentService = roleAssignmentService ?? throw new ArgumentNullException(nameof(roleAssignmentService));
        _additionalSources = additionalSources ?? Array.Empty<IAdditionalPermissionSource>();
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var basePermissions = await _roleAssignmentService
            .GetEffectivePermissionsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var permission in basePermissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                permissions.Add(permission.Trim());
            }
        }

        foreach (var source in _additionalSources)
        {
            var additional = await source.GetAdditionalPermissionsAsync(userId, cancellationToken).ConfigureAwait(false);
            if (additional is null)
            {
                continue;
            }

            foreach (var permission in additional)
            {
                if (!string.IsNullOrWhiteSpace(permission))
                {
                    permissions.Add(permission.Trim());
                }
            }
        }

        return permissions.Count == 0 ? Array.Empty<string>() : permissions.ToList();
    }
}
