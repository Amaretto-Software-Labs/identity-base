using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Roles.Services;

public interface IAdditionalPermissionSource
{
    Task<IReadOnlyCollection<string>> GetAdditionalPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
