using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Services;

namespace Identity.Base.Roles.Infrastructure;

internal sealed class IdentitySeedRoleAssignmentHandler(IRoleAssignmentService roleAssignmentService)
    : IIdentitySeedRoleAssignmentHandler
{
    public Task AssignRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        if (roleNames.Count == 0)
        {
            return Task.CompletedTask;
        }

        return roleAssignmentService.AssignRolesAsync(userId, roleNames, cancellationToken);
    }
}
