using System.Linq;
using Shouldly;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Services;

namespace Identity.Base.Organisations.Tests.Services;

public class OrganisationAdditionalPermissionSourceTests
{
    [Fact]
    public async Task GetAdditionalPermissionsAsync_ReturnsEmpty_WhenUserIdMissing()
    {
        var accessor = new OrganisationContextAccessor();
        var resolver = new StubPermissionResolver();
        var source = new OrganisationAdditionalPermissionSource(accessor, resolver);

        var permissions = await source.GetAdditionalPermissionsAsync(Guid.Empty);

        permissions.ShouldBeEmpty();
        resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAdditionalPermissionsAsync_ReturnsEmpty_WhenNoActiveOrganisation()
    {
        var accessor = new OrganisationContextAccessor();
        var resolver = new StubPermissionResolver();
        var source = new OrganisationAdditionalPermissionSource(accessor, resolver);

        var permissions = await source.GetAdditionalPermissionsAsync(Guid.NewGuid());

        permissions.ShouldBeEmpty();
        resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAdditionalPermissionsAsync_ReturnsPermissions_WhenContextActive()
    {
        var accessor = new OrganisationContextAccessor();
        var resolver = new StubPermissionResolver(["organisation.members.manage"]);
        var source = new OrganisationAdditionalPermissionSource(accessor, resolver);

        var organisationId = Guid.NewGuid();
        using (accessor.BeginScope(new OrganisationContext(organisationId, null, "acme", "Acme", OrganisationMetadata.Empty)))
        {
            var userId = Guid.NewGuid();
            var permissions = await source.GetAdditionalPermissionsAsync(userId);

            permissions.ToArray().ShouldBe(new[] { "organisation.members.manage" });
            resolver.Requests.ShouldContain(request => request.OrganisationId == organisationId && request.UserId == userId);
            resolver.Requests.Count(request => request.OrganisationId == organisationId && request.UserId == userId).ShouldBe(1);
        }
    }

    private sealed class StubPermissionResolver : IOrganisationPermissionResolver
    {
        private readonly IReadOnlyList<string> _permissions;

        public StubPermissionResolver(IReadOnlyList<string>? permissions = null)
        {
            _permissions = permissions ?? Array.Empty<string>();
        }

        public List<(Guid OrganisationId, Guid UserId)> Requests { get; } = new();

        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
        {
            Requests.Add((organisationId, userId));
            return Task.FromResult(_permissions);
        }

        public Task<IReadOnlyList<string>> GetOrganisationPermissionsAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
        {
            Requests.Add((organisationId, userId));
            return Task.FromResult(_permissions);
        }
    }
}
