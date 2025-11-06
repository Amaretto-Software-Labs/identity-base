using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Abstractions;

/// <summary>
/// Allows feature packages (e.g., RBAC) to participate in seed-user role assignments
/// without forcing the core Identity.Base package to reference those optional components.
/// </summary>
public interface IIdentitySeedRoleAssignmentHandler
{
    Task AssignRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken);
}

