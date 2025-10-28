using FluentAssertions;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Services;

namespace Identity.Base.Organizations.Tests.Services;

public class OrganizationAdditionalPermissionSourceTests
{
    [Fact]
    public async Task GetAdditionalPermissionsAsync_ReturnsEmpty_WhenUserIdMissing()
    {
        var accessor = new OrganizationContextAccessor();
        var resolver = new StubPermissionResolver();
        var source = new OrganizationAdditionalPermissionSource(accessor, resolver);

        var permissions = await source.GetAdditionalPermissionsAsync(Guid.Empty);

        permissions.Should().BeEmpty();
        resolver.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAdditionalPermissionsAsync_ReturnsEmpty_WhenNoActiveOrganization()
    {
        var accessor = new OrganizationContextAccessor();
        var resolver = new StubPermissionResolver();
        var source = new OrganizationAdditionalPermissionSource(accessor, resolver);

        var permissions = await source.GetAdditionalPermissionsAsync(Guid.NewGuid());

        permissions.Should().BeEmpty();
        resolver.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAdditionalPermissionsAsync_ReturnsPermissions_WhenContextActive()
    {
        var accessor = new OrganizationContextAccessor();
        var resolver = new StubPermissionResolver(["organization.members.manage"]);
        var source = new OrganizationAdditionalPermissionSource(accessor, resolver);

        var organizationId = Guid.NewGuid();
        using (accessor.BeginScope(new OrganizationContext(organizationId, null, "acme", "Acme", OrganizationMetadata.Empty)))
        {
            var userId = Guid.NewGuid();
            var permissions = await source.GetAdditionalPermissionsAsync(userId);

            permissions.Should().BeEquivalentTo(new[] { "organization.members.manage" });
            resolver.Requests.Should().ContainSingle(request => request.OrganizationId == organizationId && request.UserId == userId);
        }
    }

    private sealed class StubPermissionResolver : IOrganizationPermissionResolver
    {
        private readonly IReadOnlyList<string> _permissions;

        public StubPermissionResolver(IReadOnlyList<string>? permissions = null)
        {
            _permissions = permissions ?? Array.Empty<string>();
        }

        public List<(Guid OrganizationId, Guid UserId)> Requests { get; } = new();

        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
        {
            Requests.Add((organizationId, userId));
            return Task.FromResult(_permissions);
        }

        public Task<IReadOnlyList<string>> GetOrganizationPermissionsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
        {
            Requests.Add((organizationId, userId));
            return Task.FromResult(_permissions);
        }
    }
}
