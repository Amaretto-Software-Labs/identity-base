using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Services;
using Identity.Base.Organizations.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Identity.Base.Organizations.Tests.Services;

public class OrganizationInvitationServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsInvitation_WhenRolesValid()
    {
        // Arrange
        var store = new FakeInvitationStore();
        var (organization, roles) = CreateOrganization();
        var services = CreateServiceHarness(store, organization: organization, roles: roles);

        var roleIds = roles.Select(role => role.Id).ToArray();

        // Act
        var invitation = await services.Service.CreateAsync(
            organization.Id,
            "user@example.com",
            roleIds,
            createdBy: Guid.NewGuid(),
            expiresInHours: 48,
            CancellationToken.None);

        // Assert
        invitation.OrganizationId.ShouldBe(organization.Id);
        invitation.RoleIds.OrderBy(id => id).ToArray().ShouldBe(roleIds.OrderBy(id => id).ToArray());
        var storedRecord = store.Created.ShouldHaveSingleItem();
        storedRecord.Code.ShouldBe(invitation.Code);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenActiveInvitationExists()
    {
        // Arrange
        var store = new FakeInvitationStore();
        var (organization, roles) = CreateOrganization();
        var services = CreateServiceHarness(store, organization: organization, roles: roles);

        await services.Service.CreateAsync(
            organization.Id,
            "user@example.com",
            Array.Empty<Guid>(),
            createdBy: null,
            expiresInHours: null,
            CancellationToken.None);

        // Act
        Func<Task> act = () => services.Service.CreateAsync(
            organization.Id,
            "user@example.com",
            Array.Empty<Guid>(),
            createdBy: null,
            expiresInHours: null,
            CancellationToken.None);

        // Assert
        await Should.ThrowAsync<OrganizationInvitationAlreadyExistsException>(act);
    }

    [Fact]
    public async Task AcceptAsync_AddsMembership_ForNewUser()
    {
        // Arrange
        var store = new FakeInvitationStore();
        var membershipService = new FakeMembershipService();
        var (organization, roles) = CreateOrganization();
        var harness = CreateServiceHarness(
            store,
            organization: organization,
            roles: roles,
            membershipService: membershipService);

        var invitation = await harness.Service.CreateAsync(
            organization.Id,
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
        acceptance!.OrganizationId.ShouldBe(organization.Id);
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
        var (organization, roles) = CreateOrganization();
        var harness = CreateServiceHarness(
            store,
            organization: organization,
            roles: roles,
            membershipService: membershipService);

        var existingMembership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = Guid.NewGuid(),
            RoleAssignments =
            {
            new OrganizationRoleAssignment
            {
                OrganizationId = organization.Id,
                UserId = Guid.NewGuid(),
                RoleId = roles.First().Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                }
            }
        };

        membershipService.ExistingMembership = existingMembership;

        var invitation = await harness.Service.CreateAsync(
            organization.Id,
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

    private static (Organization Organization, List<OrganizationRole> Roles) CreateOrganization(Guid? organizationId = null)
    {
        var organization = new Organization
        {
            Id = organizationId ?? Guid.NewGuid(),
            Slug = "org",
            DisplayName = "Organization",
            TenantId = null
        };

        var roles = new List<OrganizationRole>
        {
            new()
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                TenantId = null,
                Name = "RoleA",
                CreatedAtUtc = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                TenantId = null,
                Name = "RoleB",
                CreatedAtUtc = DateTimeOffset.UtcNow
            }
        };

        return (organization, roles);
    }

    private static (OrganizationInvitationService Service, FakeInvitationStore Store) CreateServiceHarness(
        FakeInvitationStore? store = null,
        Organization? organization = null,
        IEnumerable<OrganizationRole>? roles = null,
        FakeMembershipService? membershipService = null)
    {
        store ??= new FakeInvitationStore();
        organization ??= CreateOrganization().Organization;
        roles ??= Enumerable.Empty<OrganizationRole>();
        membershipService ??= new FakeMembershipService();

        var organizationService = new FakeOrganizationService(organization);
        var roleService = new FakeOrganizationRoleService(roles.ToList());
        var logger = NullLogger<OrganizationInvitationService>.Instance;
        var sanitizer = new PassThroughSanitizer();

        var service = new OrganizationInvitationService(
            store,
            organizationService,
            membershipService,
            roleService,
            logger,
            sanitizer,
            NullOrganizationLifecycleDispatcher.Instance);

        return (service, store);
    }

    private static void SetCreatedAt(ApplicationUser user, DateTimeOffset value)
    {
        var property = typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.CreatedAt));
        property!.SetValue(user, value);
    }

    private sealed class FakeInvitationStore : IOrganizationInvitationStore
    {
        private readonly Dictionary<Guid, OrganizationInvitationRecord> _records = new();

        public List<OrganizationInvitationRecord> Created { get; } = new();

        public Task<OrganizationInvitationRecord> CreateAsync(OrganizationInvitationRecord invitation, CancellationToken cancellationToken = default)
        {
            _records[invitation.Code] = invitation;
            Created.Add(invitation);
            return Task.FromResult(invitation);
        }

        public Task<IReadOnlyCollection<OrganizationInvitationRecord>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            var items = _records.Values
                .Where(record => record.OrganizationId == organizationId && record.ExpiresAtUtc > DateTimeOffset.UtcNow)
                .ToList();
            return Task.FromResult<IReadOnlyCollection<OrganizationInvitationRecord>>(items);
        }

        public Task<PagedResult<OrganizationInvitationRecord>> ListAsync(
            Guid organizationId,
            PageRequest pageRequest,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pageRequest);

            var normalized = pageRequest.WithDefaults();
            var items = _records.Values
                .Where(record => record.OrganizationId == organizationId && record.ExpiresAtUtc > DateTimeOffset.UtcNow)
                .OrderBy(record => record.ExpiresAtUtc)
                .ToList();

            var total = items.Count;
            if (total == 0)
            {
                return Task.FromResult(PagedResult<OrganizationInvitationRecord>.Empty(normalized.Page, normalized.PageSize));
            }

            var paged = items
                .Skip(normalized.GetSkip())
                .Take(normalized.PageSize)
                .ToList();

            return Task.FromResult(new PagedResult<OrganizationInvitationRecord>(normalized.Page, normalized.PageSize, total, paged));
        }

        public Task<OrganizationInvitationRecord?> FindAsync(Guid code, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(code, out var record);
            return Task.FromResult(record);
        }

        public Task RemoveAsync(Guid code, CancellationToken cancellationToken = default)
        {
            _records.Remove(code);
            return Task.CompletedTask;
        }

        public Task<bool> HasActiveInvitationAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken = default)
        {
            var exists = _records.Values.Any(record =>
                record.OrganizationId == organizationId &&
                string.Equals(record.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(exists);
        }
    }

    private sealed class FakeOrganizationService : IOrganizationService
    {
        private readonly Organization _organization;

        public FakeOrganizationService(Organization organization)
        {
            _organization = organization;
        }

        public Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_organization.Id == organizationId ? _organization : null);

        public Task<Organization> CreateAsync(OrganizationCreateRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Organization?> GetBySlugAsync(Guid? tenantId, string slug, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Organization>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<Organization>> ListAsync(Guid? tenantId, PageRequest pageRequest, OrganizationStatus? status = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Organization> UpdateAsync(Guid organizationId, OrganizationUpdateRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ArchiveAsync(Guid organizationId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeOrganizationRoleService : IOrganizationRoleService
    {
        private readonly List<OrganizationRole> _roles;

        public FakeOrganizationRoleService(List<OrganizationRole> roles)
        {
            _roles = roles;
        }

        public Task<IReadOnlyList<OrganizationRole>> ListAsync(Guid? tenantId, Guid? organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrganizationRole>>(_roles);

        public Task<PagedResult<OrganizationRole>> ListAsync(Guid? tenantId, Guid? organizationId, PageRequest pageRequest, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pageRequest);
            var normalized = pageRequest.WithDefaults();
            var total = _roles.Count;
            var paged = _roles
                .Skip(normalized.GetSkip())
                .Take(normalized.PageSize)
                .ToList();

            return Task.FromResult(new PagedResult<OrganizationRole>(normalized.Page, normalized.PageSize, total, paged));
        }

        public Task<OrganizationRole> CreateAsync(OrganizationRoleCreateRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OrganizationRole?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OrganizationRolePermissionSet> GetPermissionsAsync(Guid roleId, Guid organizationId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdatePermissionsAsync(Guid roleId, Guid organizationId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeMembershipService : IOrganizationMembershipService
    {
        public OrganizationMembership? ExistingMembership { get; set; }
        public OrganizationMembershipRequest? AddedMembershipRequest { get; private set; }
        public OrganizationMembershipUpdateRequest? UpdatedMembershipRequest { get; private set; }

        public Task<OrganizationMembership> AddMemberAsync(OrganizationMembershipRequest request, CancellationToken cancellationToken = default)
        {
            AddedMembershipRequest = request;

            var membership = new OrganizationMembership
            {
                OrganizationId = request.OrganizationId,
                UserId = request.UserId,
                TenantId = request.TenantId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            if (request.RoleIds is not null)
            {
                foreach (var roleId in request.RoleIds)
                {
                    membership.RoleAssignments.Add(new OrganizationRoleAssignment
                    {
                        OrganizationId = request.OrganizationId,
                        UserId = request.UserId,
                        RoleId = roleId,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    });
                }
            }

            return Task.FromResult(membership);
        }

        public Task<OrganizationMembership?> GetMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingMembership);
        }

        public Task<OrganizationMembership> UpdateMembershipAsync(OrganizationMembershipUpdateRequest request, CancellationToken cancellationToken = default)
        {
            UpdatedMembershipRequest = request;
            return Task.FromResult(new OrganizationMembership
            {
                OrganizationId = request.OrganizationId,
                UserId = request.UserId,
                RoleAssignments =
                {
                    new OrganizationRoleAssignment { RoleId = request.RoleIds?.FirstOrDefault() ?? Guid.Empty }
                }
            });
        }

        public Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<OrganizationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<UserOrganizationMembership>> GetMembershipsForUserAsync(Guid userId, Guid? tenantId, PageRequest pageRequest, bool includeArchived, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OrganizationMemberListResult> GetMembersAsync(OrganizationMemberListRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class PassThroughSanitizer : ILogSanitizer
    {
        public string? RedactEmail(string? value) => value;

        public string? RedactPhoneNumber(string? value) => value;

        public string RedactToken(string? value) => value ?? string.Empty;
    }
}
