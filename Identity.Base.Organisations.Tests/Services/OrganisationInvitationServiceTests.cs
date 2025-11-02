using System.Linq;
using Shouldly;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Organisations.Abstractions;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.Base.Organisations.Tests.Services;

public class OrganisationInvitationServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsInvitation_WhenRolesValid()
    {
        // Arrange
        var store = new FakeInvitationStore();
        var (organisation, roles) = CreateOrganisation();
        var services = CreateServiceHarness(store, organisation: organisation, roles: roles);

        var roleIds = roles.Select(role => role.Id).ToArray();

        // Act
        var invitation = await services.Service.CreateAsync(
            organisation.Id,
            "user@example.com",
            roleIds,
            createdBy: Guid.NewGuid(),
            expiresInHours: 48,
            CancellationToken.None);

        // Assert
        invitation.OrganisationId.ShouldBe(organisation.Id);
        invitation.RoleIds.OrderBy(id => id).ToArray().ShouldBe(roleIds.OrderBy(id => id).ToArray());
        var storedRecord = store.Created.ShouldHaveSingleItem();
        storedRecord.Code.ShouldBe(invitation.Code);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenActiveInvitationExists()
    {
        // Arrange
        var store = new FakeInvitationStore();
        var (organisation, roles) = CreateOrganisation();
        var services = CreateServiceHarness(store, organisation: organisation, roles: roles);

        await services.Service.CreateAsync(
            organisation.Id,
            "user@example.com",
            Array.Empty<Guid>(),
            createdBy: null,
            expiresInHours: null,
            CancellationToken.None);

        // Act
        Func<Task> act = () => services.Service.CreateAsync(
            organisation.Id,
            "user@example.com",
            Array.Empty<Guid>(),
            createdBy: null,
            expiresInHours: null,
            CancellationToken.None);

        // Assert
        await Should.ThrowAsync<OrganisationInvitationAlreadyExistsException>(act);
    }

    [Fact]
    public async Task AcceptAsync_AddsMembership_ForNewUser()
    {
        // Arrange
        var store = new FakeInvitationStore();
        var membershipService = new FakeMembershipService();
        var (organisation, roles) = CreateOrganisation();
        var harness = CreateServiceHarness(
            store,
            organisation: organisation,
            roles: roles,
            membershipService: membershipService);

        var invitation = await harness.Service.CreateAsync(
            organisation.Id,
            "user@example.com",
            Array.Empty<Guid>(),
            createdBy: null,
            expiresInHours: null,
            CancellationToken.None);

        var user = new ApplicationUser { Email = "user@example.com" };

        // Act
        var acceptance = await harness.Service.AcceptAsync(invitation.Code, user, CancellationToken.None);

        // Assert
        acceptance.ShouldNotBeNull();
        acceptance!.OrganisationId.ShouldBe(organisation.Id);
        acceptance.WasExistingMember.ShouldBeFalse();
        membershipService.AddedMembershipRequest.ShouldNotBeNull();
        membershipService.UpdatedMembershipRequest.ShouldBeNull();
    }

    [Fact]
    public async Task AcceptAsync_MergesRoles_ForExistingMember()
    {
        // Arrange
        var store = new FakeInvitationStore();
        var membershipService = new FakeMembershipService();
        var (organisation, roles) = CreateOrganisation();
        var harness = CreateServiceHarness(
            store,
            organisation: organisation,
            roles: roles,
            membershipService: membershipService);

        var existingMembership = new OrganisationMembership
        {
            OrganisationId = organisation.Id,
            UserId = Guid.NewGuid(),
            RoleAssignments =
            {
            new OrganisationRoleAssignment
            {
                OrganisationId = organisation.Id,
                UserId = Guid.NewGuid(),
                RoleId = roles.First().Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                }
            }
        };

        membershipService.ExistingMembership = existingMembership;

        var invitation = await harness.Service.CreateAsync(
            organisation.Id,
            "existing@example.com",
            roles.Select(role => role.Id).ToArray(),
            createdBy: null,
            expiresInHours: null,
            CancellationToken.None);

        var existingUser = new ApplicationUser { Email = "existing@example.com" };
        SetCreatedAt(existingUser, invitation.CreatedAtUtc.AddMinutes(-5));

        // Act
        var acceptance = await harness.Service.AcceptAsync(invitation.Code, existingUser, CancellationToken.None);

        // Assert
        acceptance.ShouldNotBeNull();
        acceptance!.WasExistingMember.ShouldBeTrue();
        membershipService.AddedMembershipRequest.ShouldBeNull();
        membershipService.UpdatedMembershipRequest.ShouldNotBeNull();
        membershipService.UpdatedMembershipRequest!.RoleIds!
            .OrderBy(id => id)
            .ToArray()
            .ShouldBe(roles.Select(role => role.Id).OrderBy(id => id).ToArray());
    }

    private static (Organisation Organisation, List<OrganisationRole> Roles) CreateOrganisation(Guid? organisationId = null)
    {
        var organisation = new Organisation
        {
            Id = organisationId ?? Guid.NewGuid(),
            Slug = "org",
            DisplayName = "Organisation",
            TenantId = null
        };

        var roles = new List<OrganisationRole>
        {
            new()
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisation.Id,
                TenantId = null,
                Name = "RoleA",
                CreatedAtUtc = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                OrganisationId = organisation.Id,
                TenantId = null,
                Name = "RoleB",
                CreatedAtUtc = DateTimeOffset.UtcNow
            }
        };

        return (organisation, roles);
    }

    private static (OrganisationInvitationService Service, FakeInvitationStore Store) CreateServiceHarness(
        FakeInvitationStore? store = null,
        Organisation? organisation = null,
        IEnumerable<OrganisationRole>? roles = null,
        FakeMembershipService? membershipService = null)
    {
        store ??= new FakeInvitationStore();
        organisation ??= CreateOrganisation().Organisation;
        roles ??= Enumerable.Empty<OrganisationRole>();
        membershipService ??= new FakeMembershipService();

        var organisationService = new FakeOrganisationService(organisation);
        var roleService = new FakeOrganisationRoleService(roles.ToList());
        var logger = NullLogger<OrganisationInvitationService>.Instance;
        var sanitizer = new PassThroughSanitizer();

        var service = new OrganisationInvitationService(
            store,
            organisationService,
            membershipService,
            roleService,
            logger,
            sanitizer);

        return (service, store);
    }

    private static void SetCreatedAt(ApplicationUser user, DateTimeOffset value)
    {
        var property = typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.CreatedAt));
        property!.SetValue(user, value);
    }

    private sealed class FakeInvitationStore : IOrganisationInvitationStore
    {
        private readonly Dictionary<Guid, OrganisationInvitationRecord> _records = new();

        public List<OrganisationInvitationRecord> Created { get; } = new();

        public Task<OrganisationInvitationRecord> CreateAsync(OrganisationInvitationRecord invitation, CancellationToken cancellationToken = default)
        {
            _records[invitation.Code] = invitation;
            Created.Add(invitation);
            return Task.FromResult(invitation);
        }

        public Task<IReadOnlyCollection<OrganisationInvitationRecord>> ListAsync(Guid organisationId, CancellationToken cancellationToken = default)
        {
            var items = _records.Values
                .Where(record => record.OrganisationId == organisationId && record.ExpiresAtUtc > DateTimeOffset.UtcNow)
                .ToList();
            return Task.FromResult<IReadOnlyCollection<OrganisationInvitationRecord>>(items);
        }

        public Task<OrganisationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(code, out var record);
            return Task.FromResult(record);
        }

        public Task RemoveAsync(Guid code, CancellationToken cancellationToken = default)
        {
            _records.Remove(code);
            return Task.CompletedTask;
        }

        public Task<bool> HasActiveInvitationAsync(Guid organisationId, string normalizedEmail, CancellationToken cancellationToken = default)
        {
            var exists = _records.Values.Any(record =>
                record.OrganisationId == organisationId &&
                string.Equals(record.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(exists);
        }
    }

    private sealed class FakeOrganisationService : IOrganisationService
    {
        private readonly Organisation _organisation;

        public FakeOrganisationService(Organisation organisation)
        {
            _organisation = organisation;
        }

        public Task<Organisation?> GetByIdAsync(Guid organisationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_organisation.Id == organisationId ? _organisation : null);

        public Task<Organisation> CreateAsync(OrganisationCreateRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Organisation?> GetBySlugAsync(Guid? tenantId, string slug, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Organisation>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Organisation> UpdateAsync(Guid organisationId, OrganisationUpdateRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ArchiveAsync(Guid organisationId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeOrganisationRoleService : IOrganisationRoleService
    {
        private readonly List<OrganisationRole> _roles;

        public FakeOrganisationRoleService(List<OrganisationRole> roles)
        {
            _roles = roles;
        }

        public Task<IReadOnlyList<OrganisationRole>> ListAsync(Guid? tenantId, Guid? organisationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrganisationRole>>(_roles);

        public Task<OrganisationRole> CreateAsync(OrganisationRoleCreateRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OrganisationRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OrganisationRolePermissionSet> GetPermissionsAsync(Guid roleId, Guid organisationId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdatePermissionsAsync(Guid roleId, Guid organisationId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeMembershipService : IOrganisationMembershipService
    {
        public OrganisationMembership? ExistingMembership { get; set; }
        public OrganisationMembershipRequest? AddedMembershipRequest { get; private set; }
        public OrganisationMembershipUpdateRequest? UpdatedMembershipRequest { get; private set; }

        public Task<OrganisationMembership> AddMemberAsync(OrganisationMembershipRequest request, CancellationToken cancellationToken = default)
        {
            AddedMembershipRequest = request;

            var membership = new OrganisationMembership
            {
                OrganisationId = request.OrganisationId,
                UserId = request.UserId,
                TenantId = request.TenantId,
                IsPrimary = request.IsPrimary,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            if (request.RoleIds is not null)
            {
                foreach (var roleId in request.RoleIds)
                {
                    membership.RoleAssignments.Add(new OrganisationRoleAssignment
                    {
                        OrganisationId = request.OrganisationId,
                        UserId = request.UserId,
                        RoleId = roleId,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    });
                }
            }

            return Task.FromResult(membership);
        }

        public Task<OrganisationMembership?> GetMembershipAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingMembership);
        }

        public Task<OrganisationMembership> UpdateMembershipAsync(OrganisationMembershipUpdateRequest request, CancellationToken cancellationToken = default)
        {
            UpdatedMembershipRequest = request;
            return Task.FromResult(new OrganisationMembership
            {
                OrganisationId = request.OrganisationId,
                UserId = request.UserId,
                RoleAssignments =
                {
                    new OrganisationRoleAssignment { RoleId = request.RoleIds?.FirstOrDefault() ?? Guid.Empty }
                }
            });
        }

        public Task RemoveMemberAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<OrganisationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OrganisationMemberListResult> GetMembersAsync(OrganisationMemberListRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class PassThroughSanitizer : ILogSanitizer
    {
        public string? RedactEmail(string? value) => value;

        public string? RedactPhoneNumber(string? value) => value;

        public string RedactToken(string? value) => value ?? string.Empty;
    }
}
