using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Roles.Services;

namespace Identity.Base.Organisations.Services;

public sealed class OrganisationAdditionalPermissionSource : IAdditionalPermissionSource
{
    private readonly IOrganisationContextAccessor _organisationContextAccessor;
    private readonly IOrganisationPermissionResolver _permissionResolver;

    public OrganisationAdditionalPermissionSource(
        IOrganisationContextAccessor organisationContextAccessor,
        IOrganisationPermissionResolver permissionResolver)
    {
        _organisationContextAccessor = organisationContextAccessor ?? throw new ArgumentNullException(nameof(organisationContextAccessor));
        _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
    }

    public async Task<IReadOnlyCollection<string>> GetAdditionalPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var context = _organisationContextAccessor.Current;
        if (context is null || !context.OrganisationId.HasValue || context.OrganisationId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var permissions = await _permissionResolver
            .GetOrganisationPermissionsAsync(context.OrganisationId.Value, userId, cancellationToken)
            .ConfigureAwait(false);

        return permissions;
    }
}
