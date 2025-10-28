using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Identity.Base.Roles.Services;

namespace Identity.Base.Tests.Roles;

public class CompositePermissionResolverTests
{
    [Fact]
    public async Task GetEffectivePermissionsAsync_UnionsBaseAndAdditionalSources()
    {
        var baseService = new StubRoleAssignmentService(["users.read", "users.update"]);
        var additionalSources = new IAdditionalPermissionSource[]
        {
            new StubAdditionalPermissionSource(["organizations.read"]),
            new StubAdditionalPermissionSource(["organization.members.manage"])
        };

        var resolver = new CompositePermissionResolver(baseService, additionalSources);
        var permissions = await resolver.GetEffectivePermissionsAsync(Guid.NewGuid());

        permissions.ToArray().ShouldBe(new[]
        {
            "users.read",
            "users.update",
            "organizations.read",
            "organization.members.manage"
        });
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_DeduplicatesPermissions()
    {
        var userId = Guid.NewGuid();
        var baseService = new StubRoleAssignmentService(["users.read"]);
        var additionalSources = new IAdditionalPermissionSource[]
        {
            new StubAdditionalPermissionSource(["users.read", "users.manage"])
        };

        var resolver = new CompositePermissionResolver(baseService, additionalSources);
        var permissions = await resolver.GetEffectivePermissionsAsync(userId);

        permissions.ToArray().ShouldBe(new[] { "users.read", "users.manage" });
    }

    private sealed class StubRoleAssignmentService : IRoleAssignmentService
    {
        private readonly IReadOnlyList<string> _permissions;

        public StubRoleAssignmentService(IReadOnlyList<string> permissions)
        {
            _permissions = permissions;
        }

        public Task AssignRolesAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetUserRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_permissions);
    }

    private sealed class StubAdditionalPermissionSource : IAdditionalPermissionSource
    {
        private readonly IReadOnlyCollection<string> _permissions;

        public StubAdditionalPermissionSource(IReadOnlyCollection<string> permissions)
        {
            _permissions = permissions;
        }

        public Task<IReadOnlyCollection<string>> GetAdditionalPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_permissions);
    }
}
