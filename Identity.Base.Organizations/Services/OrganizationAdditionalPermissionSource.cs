using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Roles.Services;

namespace Identity.Base.Organizations.Services;

public sealed class OrganizationAdditionalPermissionSource : IAdditionalPermissionSource
{
    private readonly IOrganizationContextAccessor _organizationContextAccessor;
    private readonly IOrganizationPermissionResolver _permissionResolver;

    public OrganizationAdditionalPermissionSource(
        IOrganizationContextAccessor organizationContextAccessor,
        IOrganizationPermissionResolver permissionResolver)
    {
        _organizationContextAccessor = organizationContextAccessor ?? throw new ArgumentNullException(nameof(organizationContextAccessor));
        _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
    }

    public async Task<IReadOnlyCollection<string>> GetAdditionalPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var context = _organizationContextAccessor.Current;
        if (context is null || !context.OrganizationId.HasValue || context.OrganizationId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var permissions = await _permissionResolver
            .GetOrganizationPermissionsAsync(context.OrganizationId.Value, userId, cancellationToken)
            .ConfigureAwait(false);

        return permissions;
    }
}
