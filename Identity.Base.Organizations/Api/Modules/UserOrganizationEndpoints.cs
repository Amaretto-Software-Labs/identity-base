using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Identity.Base.Abstractions.Pagination;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Api.Models;
using Identity.Base.Organizations.Authorization;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Options;
using Identity.Base.Organizations.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Base.Organizations.Api.Modules;

public static class UserOrganizationEndpoints
{
    public static IEndpointRouteBuilder MapUserOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var baseGroup = endpoints.MapGroup("/users/me/organizations");

        baseGroup.MapPost(string.Empty, async (
            CreateOrganizationRequest request,
            IValidator<CreateOrganizationRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationService organizationService,
            IOrganizationMembershipService membershipService,
            OrganizationDbContext dbContext,
            IOptions<OrganizationRoleOptions> roleOptions,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var ownerRoleId = await ResolveOwnerRoleIdAsync(dbContext, roleOptions.Value, cancellationToken).ConfigureAwait(false);
            if (ownerRoleId is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Organization role missing",
                    detail: "Default organization owner role is not available. Ensure organization roles are seeded.");
            }

            try
            {
                var organization = await organizationService.CreateAsync(new OrganizationCreateRequest
                {
                    TenantId = request.TenantId,
                    Slug = request.Slug,
                    DisplayName = request.DisplayName,
                    Metadata = request.Metadata
                }, cancellationToken).ConfigureAwait(false);

                await membershipService.AddMemberAsync(new OrganizationMembershipRequest
                {
                    OrganizationId = organization.Id,
                    UserId = userId,
                    RoleIds = new[] { ownerRoleId.Value }
                }, cancellationToken).ConfigureAwait(false);

                var dto = OrganizationApiMapper.ToOrganizationDto(organization);
                return Results.Created($"/users/me/organizations/{organization.Id}", dto);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organization request", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Organization conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization();

        baseGroup.MapGet(string.Empty, async (
            ClaimsPrincipal principal,
            Guid? tenantId,
            [AsParameters] UserOrganizationListQuery query,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var pageRequest = query.ToPageRequest();
            var includeArchived = query.IncludeArchived ?? false;

            var result = await membershipService
                .GetMembershipsForUserAsync(userId, tenantId, pageRequest, includeArchived, cancellationToken)
                .ConfigureAwait(false);

            var items = result.Items
                .Select(OrganizationApiMapper.ToUserOrganizationMembershipDto)
                .ToList();

            var response = new PagedResult<UserOrganizationMembershipDto>(
                result.Page,
                result.PageSize,
                result.TotalCount,
                items);

            return Results.Ok(response);
        })
        .RequireAuthorization();

        var organizationGroup = baseGroup.MapGroup("/{organizationId:guid}");

        organizationGroup.MapGet(string.Empty, async (
            Guid organizationId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var organization = await organizationService.GetByIdAsync(organizationId, cancellationToken).ConfigureAwait(false);
            return organization is null
                ? Results.NotFound()
                : Results.Ok(OrganizationApiMapper.ToOrganizationDto(organization));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationsRead));

        organizationGroup.MapPatch(string.Empty, async (
            Guid organizationId,
            UpdateOrganizationRequest request,
            IValidator<UpdateOrganizationRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var updated = await organizationService.UpdateAsync(organizationId, new OrganizationUpdateRequest
                {
                    DisplayName = request.DisplayName,
                    Metadata = request.Metadata,
                    Status = request.Status
                }, cancellationToken).ConfigureAwait(false);

                return Results.Ok(OrganizationApiMapper.ToOrganizationDto(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid organization update", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Organization conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationsManage));

        MapUserMemberEndpoints(organizationGroup);
        MapUserRoleEndpoints(organizationGroup);
        MapUserInvitationEndpoints(organizationGroup);

        return endpoints;
    }

    private static void MapUserMemberEndpoints(RouteGroupBuilder organizationGroup)
    {
        var memberGroup = organizationGroup.MapGroup("/members");

        memberGroup.MapGet(string.Empty, async (
            Guid organizationId,
            [AsParameters] OrganizationMemberListQuery query,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var request = new OrganizationMemberListRequest
            {
                OrganizationId = organizationId,
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search,
                RoleId = query.RoleId,
                Sort = ResolveSort(query.Sort)
            };

            var members = await membershipService.GetMembersAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(OrganizationApiMapper.ToMemberPagedResult(members));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersRead));

        memberGroup.MapPost(string.Empty, async (
            Guid organizationId,
            AddMembershipRequest request,
            IValidator<AddMembershipRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, request.UserId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var membership = await membershipService.AddMemberAsync(new OrganizationMembershipRequest
                {
                    OrganizationId = organizationId,
                    UserId = request.UserId,
                    TenantId = null,
                    RoleIds = request.RoleIds
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/users/me/organizations/{organizationId}/members/{membership.UserId}", OrganizationApiMapper.ToMembershipDto(membership));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid membership", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Membership conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organization not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));

        memberGroup.MapPut("/{userId:guid}", async (
            Guid organizationId,
            Guid userId,
            UpdateMembershipRequest request,
            IValidator<UpdateMembershipRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, userId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var membership = await membershipService.UpdateMembershipAsync(new OrganizationMembershipUpdateRequest
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    RoleIds = request.RoleIds
                }, cancellationToken).ConfigureAwait(false);

                return Results.Ok(OrganizationApiMapper.ToMembershipDto(membership));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid membership update", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Membership not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Membership conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));

        memberGroup.MapDelete("/{userId:guid}", async (
            Guid organizationId,
            Guid userId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationMembershipService membershipService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, userId, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            await membershipService.RemoveMemberAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));
    }

    private static void MapUserRoleEndpoints(RouteGroupBuilder organizationGroup)
    {
        var roleGroup = organizationGroup.MapGroup("/roles");

        roleGroup.MapGet(string.Empty, async (
            Guid organizationId,
            Guid? tenantId,
            [AsParameters] OrganizationRoleListQuery query,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var pageRequest = query.ToPageRequest();
            var roles = await roleService.ListAsync(tenantId, organizationId, pageRequest, cancellationToken).ConfigureAwait(false);
            return Results.Ok(OrganizationApiMapper.ToOrganizationRolePagedResult(roles));
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationRolesRead));

        roleGroup.MapPost(string.Empty, async (
            Guid organizationId,
            CreateOrganizationRoleRequest request,
            IValidator<CreateOrganizationRoleRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var role = await roleService.CreateAsync(new OrganizationRoleCreateRequest
                {
                    OrganizationId = organizationId,
                    TenantId = null,
                    Name = request.Name,
                    Description = request.Description,
                    IsSystemRole = request.IsSystemRole
                }, cancellationToken).ConfigureAwait(false);

                return Results.Created($"/users/me/organizations/{organizationId}/roles/{role.Id}", OrganizationApiMapper.ToRoleDto(role));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Invalid role", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Role conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organization not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationRolesManage));

        roleGroup.MapDelete("/{roleId:guid}", async (
            Guid organizationId,
            Guid roleId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                await roleService.DeleteAsync(roleId, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Role conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationRolesManage));

        roleGroup.MapGet("/{roleId:guid}/permissions", async (
            Guid organizationId,
            Guid roleId,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                var permissionSet = await roleService.GetPermissionsAsync(roleId, organizationId, cancellationToken).ConfigureAwait(false);
                return Results.Ok(OrganizationApiMapper.ToRolePermissionsResponse(permissionSet));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new ProblemDetails { Title = "Role not found", Status = StatusCodes.Status404NotFound });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationRolesRead));

        roleGroup.MapPut("/{roleId:guid}/permissions", async (
            Guid organizationId,
            Guid roleId,
            UpdateOrganizationRolePermissionsRequest request,
            IValidator<UpdateOrganizationRolePermissionsRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            IOrganizationRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            try
            {
                await roleService.UpdatePermissionsAsync(roleId, organizationId, request.Permissions, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ProblemDetails { Title = "Resource not found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new ProblemDetails { Title = "Role conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationRolesManage));
    }

    private static void MapUserInvitationEndpoints(RouteGroupBuilder organizationGroup)
    {
        var invitationGroup = organizationGroup.MapGroup("/invitations");

        invitationGroup.MapGet(string.Empty, async (
            Guid organizationId,
            [AsParameters] OrganizationInvitationListQuery query,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            OrganizationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var pageRequest = query.ToPageRequest();
            var invitations = await invitationService.ListAsync(organizationId, pageRequest, cancellationToken).ConfigureAwait(false);
            var response = OrganizationApiMapper.ToInvitationPagedResult(invitations);
            return Results.Ok(response);
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));

        invitationGroup.MapPost(string.Empty, async (
            Guid organizationId,
            CreateOrganizationInvitationRequest request,
            IValidator<CreateOrganizationInvitationRequest> validator,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            OrganizationInvitationService invitationService,
            IOrganizationMembershipService membershipService,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var emailAttribute = new EmailAddressAttribute();
            if (!emailAttribute.IsValid(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = new[] { "Email format is invalid." } });
            }

            var normalizedEmail = userManager.NormalizeEmail(request.Email);
            var existingUser = string.IsNullOrWhiteSpace(normalizedEmail)
                ? null
                : await userManager.FindByEmailAsync(normalizedEmail).ConfigureAwait(false);

            if (existingUser is not null)
            {
                var membership = await membershipService.GetMembershipAsync(organizationId, existingUser.Id, cancellationToken).ConfigureAwait(false);
                if (membership is not null)
                {
                    return Results.Conflict(new ProblemDetails
                    {
                        Title = "User already a member",
                        Detail = "The specified user is already part of this organization.",
                        Status = StatusCodes.Status409Conflict
                    });
                }
            }

            var emailToUse = normalizedEmail ?? request.Email.Trim();
            if (string.IsNullOrWhiteSpace(emailToUse))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid email",
                    Detail = "Email address normalization failed.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var actorId = GetUserId(principal);

            try
            {
                var invitation = await invitationService.CreateAsync(
                    organizationId,
                    emailToUse,
                    request.RoleIds ?? Array.Empty<Guid>(),
                    actorId,
                    request.ExpiresInHours,
                    cancellationToken).ConfigureAwait(false);

                var dto = OrganizationApiMapper.ToInvitationDto(invitation);
                return Results.Created($"/users/me/organizations/{organizationId}/invitations/{dto.Code}", dto);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new ProblemDetails { Title = "Organization not found", Status = StatusCodes.Status404NotFound });
            }
            catch (OrganizationInvitationAlreadyExistsException ex)
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Invitation already exists",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = new[] { ex.Message } });
            }
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));

        invitationGroup.MapDelete("/{code:guid}", async (
            Guid organizationId,
            Guid code,
            ClaimsPrincipal principal,
            IOrganizationScopeResolver scopeResolver,
            OrganizationInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            var scopeResult = await EnsureActorInScopeAsync(principal, scopeResolver, organizationId, null, cancellationToken).ConfigureAwait(false);
            if (scopeResult is not null)
            {
                return scopeResult;
            }

            var revoked = await invitationService.RevokeAsync(organizationId, code, cancellationToken).ConfigureAwait(false);
            return revoked ? Results.NoContent() : Results.NotFound();
        })
        .RequireAuthorization(policy => policy.RequireOrganizationPermission(UserOrganizationPermissions.OrganizationMembersManage));
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
        => TryGetUserId(principal, out var userId) ? userId : null;

    private static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganizationScopeResolver scopeResolver,
        Guid organizationId,
        Guid? targetUserId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        if (organizationId == Guid.Empty)
        {
            return Results.BadRequest(new ProblemDetails { Title = "Invalid organization", Detail = "Organization identifier is required.", Status = StatusCodes.Status400BadRequest });
        }

        var inScope = await scopeResolver.IsInScopeAsync(actorUserId, organizationId, cancellationToken).ConfigureAwait(false);
        var isSelfTargeting = targetUserId.HasValue && targetUserId.Value == actorUserId;
        if (!inScope && !isSelfTargeting)
        {
            return Results.Forbid();
        }

        return null;
    }

    private static async Task<Guid?> ResolveOwnerRoleIdAsync(
        OrganizationDbContext dbContext,
        OrganizationRoleOptions roleOptions,
        CancellationToken cancellationToken)
    {
        if (dbContext is null)
        {
            return null;
        }

        var ownerRoleName = roleOptions.DefaultRoles?
            .FirstOrDefault(role => role.DefaultType == OrganizationRoleDefaultType.Owner)
            ?.Name;

        ownerRoleName = string.IsNullOrWhiteSpace(ownerRoleName) ? "OrgOwner" : ownerRoleName.Trim();

        if (string.IsNullOrWhiteSpace(ownerRoleName))
        {
            return null;
        }

        var query = dbContext.OrganizationRoles
            .AsNoTracking()
            .Where(role => role.OrganizationId == null && role.TenantId == null);

        var inMemory = dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

        var ownerRole = inMemory
            ? query.AsEnumerable()
                .FirstOrDefault(role => string.Equals(role.Name, ownerRoleName, StringComparison.OrdinalIgnoreCase))
            : await query
                .FirstOrDefaultAsync(role => EF.Functions.ILike(role.Name, ownerRoleName), cancellationToken)
                .ConfigureAwait(false);

        return ownerRole?.Id;
    }

    private static OrganizationMemberSort ResolveSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrganizationMemberSort.CreatedAtDescending;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "createdat" => OrganizationMemberSort.CreatedAtAscending,
            "createdat:asc" => OrganizationMemberSort.CreatedAtAscending,
            "createdat:desc" => OrganizationMemberSort.CreatedAtDescending,
            "-createdat" => OrganizationMemberSort.CreatedAtDescending,
            _ => OrganizationMemberSort.CreatedAtDescending
        };
    }
}
