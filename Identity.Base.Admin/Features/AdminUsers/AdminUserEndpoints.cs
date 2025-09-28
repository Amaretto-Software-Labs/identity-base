using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Admin.Authorization;
using Identity.Base.Admin.Configuration;
using Identity.Base.Data;
using Identity.Base.Extensions;
using Identity.Base.Features.Authentication.EmailManagement;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Options;
using Identity.Base.Roles.Abstractions;
using Identity.Base.Roles.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Identity.Base.Admin.Features.AdminUsers;

    public static class AdminUserEndpoints
    {
        private static readonly DateTimeOffset SoftDeleteLockoutEnd = new(9999, 12, 31, 23, 59, 59, TimeSpan.Zero);
        private const int DefaultLockoutDays = 30;

    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/admin/users")
            .WithTags("Admin.Users");

        group.MapGet("", ListUsersAsync)
            .WithName("AdminListUsers")
            .WithSummary("Returns a paged list of users for administration.")
            .Produces<AdminUserListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.read"));

        group.MapGet("/{id:guid}", GetUserAsync)
            .WithName("AdminGetUser")
            .WithSummary("Returns details for a specific user, including roles and MFA state.")
            .Produces<AdminUserDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.read"));

        group.MapPost("", CreateUserAsync)
            .WithName("AdminCreateUser")
            .WithSummary("Creates a new user with optional roles and invitation emails.")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.create"));

        group.MapPut("/{id:guid}", UpdateUserAsync)
            .WithName("AdminUpdateUser")
            .WithSummary("Updates user profile flags and metadata.")
            .Produces<AdminUserDetailResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.update"));

        group.MapPost("/{id:guid}/lock", LockUserAsync)
            .WithName("AdminLockUser")
            .WithSummary("Locks a user account until explicitly unlocked.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.lock"));

        group.MapPost("/{id:guid}/unlock", UnlockUserAsync)
            .WithName("AdminUnlockUser")
            .WithSummary("Clears the lockout state for a user account.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.lock"));

        group.MapPost("/{id:guid}/force-password-reset", ForcePasswordResetAsync)
            .WithName("AdminForcePasswordReset")
            .WithSummary("Generates a password reset token and sends invitation email to the user.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.reset-password"));

        group.MapPost("/{id:guid}/mfa/reset", ResetMfaAsync)
            .WithName("AdminResetMfa")
            .WithSummary("Disables MFA for the user and resets the authenticator key.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.reset-mfa"));

        group.MapPost("/{id:guid}/resend-confirmation", ResendConfirmationEmailAsync)
            .WithName("AdminResendConfirmation")
            .WithSummary("Resends the account confirmation email if the user is unconfirmed.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.update"));

        group.MapGet("/{id:guid}/roles", GetUserRolesAsync)
            .WithName("AdminGetUserRoles")
            .WithSummary("Returns the set of roles currently assigned to the user.")
            .Produces<AdminUserRolesResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.manage-roles"));

        group.MapPut("/{id:guid}/roles", UpdateUserRolesAsync)
            .WithName("AdminUpdateUserRoles")
            .WithSummary("Replaces the role assignments for the user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.manage-roles"));

        group.MapDelete("/{id:guid}", SoftDeleteUserAsync)
            .WithName("AdminSoftDeleteUser")
            .WithSummary("Soft deletes the user by disabling access.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.delete"));

        group.MapPost("/{id:guid}/restore", RestoreUserAsync)
            .WithName("AdminRestoreUser")
            .WithSummary("Restores a previously soft-deleted user.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireAdminPermission("users.delete"));

        return group;
    }

    private static async Task<IResult> ListUsersAsync(
        [AsParameters] AdminUserListQuery query,
        AppDbContext appDbContext,
        IRoleDbContext roleDbContext,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 25,
            > MaxPageSize => MaxPageSize,
            _ => query.PageSize
        };

        var usersQuery = appDbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            usersQuery = usersQuery.Where(user =>
                (user.Email != null && user.Email.ToLower().Contains(term)) ||
                (user.DisplayName != null && user.DisplayName.ToLower().Contains(term)));
        }

        if (query.Locked.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            if (query.Locked.Value)
            {
                usersQuery = usersQuery.Where(user =>
                    user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > now);
            }
            else
            {
                usersQuery = usersQuery.Where(user =>
                    !user.LockoutEnabled || !user.LockoutEnd.HasValue || user.LockoutEnd <= now);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var roleName = query.Role.Trim();
            var roleId = await roleDbContext.Roles
                .Where(role => role.Name == roleName)
                .Select(role => role.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (roleId == Guid.Empty)
            {
                return Results.Ok(AdminUserListResponse.Empty(page, pageSize));
            }

            var userIdsWithRole = await roleDbContext.UserRoles
                .Where(userRole => userRole.RoleId == roleId)
                .Select(userRole => userRole.UserId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (userIdsWithRole.Count == 0)
            {
                return Results.Ok(AdminUserListResponse.Empty(page, pageSize));
            }

            usersQuery = usersQuery.Where(user => userIdsWithRole.Contains(user.Id));
        }

        var total = await usersQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var skip = (page - 1) * pageSize;
        var users = await usersQuery
            .OrderBy(user => user.Email ?? user.UserName)
            .ThenBy(user => user.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = users.Select(user => user.Id).ToList();
        var rolesLookup = new Dictionary<Guid, IReadOnlyList<string>>();

        if (userIds.Count > 0)
        {
            var roleAssignments = await roleDbContext.UserRoles
                .Where(userRole => userIds.Contains(userRole.UserId))
                .Join(
                    roleDbContext.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, role.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            rolesLookup = roleAssignments
                .GroupBy(entry => entry.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .Select(entry => entry.Name)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList());
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var items = users.Select(user =>
        {
            var roles = rolesLookup.TryGetValue(user.Id, out var value)
                ? value
                : Array.Empty<string>();

            var isLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > nowUtc;

            return new AdminUserSummary(
                user.Id,
                user.Email,
                user.DisplayName,
                user.EmailConfirmed,
                isLockedOut,
                user.CreatedAt,
                user.TwoFactorEnabled,
                roles,
                IsSoftDeleted(user));
        }).ToList();

        return Results.Ok(new AdminUserListResponse(page, pageSize, total, items));
    }

    private static async Task<IResult> GetUserAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IRoleAssignmentService roleAssignmentService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Results.NotFound();
        }

        var roles = await roleAssignmentService
            .GetUserRoleNamesAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        var externalLogins = await userManager.GetLoginsAsync(user);
        var loginDtos = externalLogins
            .Select(login => new AdminUserExternalLogin(login.LoginProvider, login.ProviderDisplayName ?? login.LoginProvider, login.ProviderKey))
            .ToList();

        var authenticatorKey = await userManager.GetAuthenticatorKeyAsync(user);
        var isLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

        var response = new AdminUserDetailResponse(
            user.Id,
            user.Email,
            user.EmailConfirmed,
            user.DisplayName,
            user.CreatedAt,
            user.LockoutEnabled,
            isLockedOut,
            user.LockoutEnd,
            user.TwoFactorEnabled,
            user.PhoneNumberConfirmed,
            user.PhoneNumber,
            user.ProfileMetadata.Values,
            user.ConcurrencyStamp ?? string.Empty,
            roles,
            loginDtos,
            !string.IsNullOrEmpty(authenticatorKey),
            IsSoftDeleted(user));

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateUserAsync(
        AdminUserCreateRequest request,
        UserManager<ApplicationUser> userManager,
        IRoleAssignmentService roleAssignmentService,
        IAccountEmailService accountEmailService,
        IOptions<RegistrationOptions> registrationOptions,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Email"] = new[] { "Email is required." }
            });
        }

        var options = registrationOptions.Value;

        var metadataSource = request.Metadata is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(request.Metadata, StringComparer.OrdinalIgnoreCase);

        var displayField = options.ProfileFields.FirstOrDefault(field => field.Name.Equals("displayName", StringComparison.OrdinalIgnoreCase));
        if (displayField is not null && !metadataSource.ContainsKey(displayField.Name))
        {
            metadataSource[displayField.Name] = string.IsNullOrWhiteSpace(request.DisplayName)
                ? normalizedEmail
                : request.DisplayName;
        }

        if (!TryNormalizeMetadata(metadataSource, options, out var metadata, out var metadataErrors))
        {
            return Results.ValidationProblem(metadataErrors);
        }

        var normalizedRoles = NormalizeRoleNames(request.Roles);

        var user = new ApplicationUser
        {
            Email = normalizedEmail,
            UserName = normalizedEmail,
            EmailConfirmed = request.EmailConfirmed,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim()
        };

        user.SetProfileMetadata(metadata);
        if (string.IsNullOrWhiteSpace(user.DisplayName) && metadata.TryGetValue("displayName", out var fromMetadata) && !string.IsNullOrWhiteSpace(fromMetadata))
        {
            user.DisplayName = fromMetadata;
        }

        IdentityResult createResult;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            createResult = await userManager.CreateAsync(user, request.Password);
        }
        else
        {
            createResult = await userManager.CreateAsync(user);
        }

        if (!createResult.Succeeded)
        {
            return Results.ValidationProblem(createResult.ToDictionary());
        }

        if (normalizedRoles.Count > 0)
        {
            await roleAssignmentService.AssignRolesAsync(user.Id, normalizedRoles, cancellationToken).ConfigureAwait(false);
        }

        if (request.SendConfirmationEmail && !user.EmailConfirmed)
        {
            await accountEmailService.SendConfirmationEmailAsync(user, cancellationToken).ConfigureAwait(false);
        }

        if (request.SendPasswordResetEmail)
        {
            await accountEmailService.SendPasswordResetEmailAsync(user, cancellationToken).ConfigureAwait(false);
        }

        var roles = await roleAssignmentService.GetUserRoleNamesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserCreated,
            user.Id,
            new
            {
                user.Email,
                Roles = roles,
                user.EmailConfirmed
            },
            cancellationToken).ConfigureAwait(false);

        return Results.Created($"/admin/users/{user.Id:D}", new
        {
            user.Id,
            user.Email,
            user.DisplayName
        });
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid id,
        AdminUserUpdateRequest request,
        UserManager<ApplicationUser> userManager,
        IRoleAssignmentService roleAssignmentService,
        IOptions<RegistrationOptions> registrationOptions,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        if (!string.Equals(user.ConcurrencyStamp, request.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return Results.Problem("User was modified by another process.", statusCode: StatusCodes.Status409Conflict);
        }

        var options = registrationOptions.Value;
        var metadataSource = new Dictionary<string, string?>(user.ProfileMetadata.Values, StringComparer.OrdinalIgnoreCase);
        if (request.Metadata is not null)
        {
            foreach (var pair in request.Metadata)
            {
                metadataSource[pair.Key] = pair.Value;
            }
        }

        if (!TryNormalizeMetadata(metadataSource, options, out var metadata, out var metadataErrors))
        {
            return Results.ValidationProblem(metadataErrors);
        }
        user.SetProfileMetadata(metadata);

        if (request.DisplayName is { Length: > 0 })
        {
            user.DisplayName = request.DisplayName.Trim();
        }
        else if (metadata.TryGetValue("displayName", out var fromMetadata) && !string.IsNullOrWhiteSpace(fromMetadata))
        {
            user.DisplayName = fromMetadata;
        }

        if (request.EmailConfirmed.HasValue)
        {
            user.EmailConfirmed = request.EmailConfirmed.Value;
        }

        if (request.LockoutEnabled.HasValue)
        {
            user.LockoutEnabled = request.LockoutEnabled.Value;
        }

        if (request.LockoutEnd.HasValue)
        {
            var lockoutEndResult = await userManager.SetLockoutEndDateAsync(user, request.LockoutEnd);
            var issue = EnsureSuccess(lockoutEndResult);
            if (issue is not null)
            {
                return issue;
            }
        }

        if (request.TwoFactorEnabled.HasValue)
        {
            var twoFactorResult = await userManager.SetTwoFactorEnabledAsync(user, request.TwoFactorEnabled.Value);
            var issue = EnsureSuccess(twoFactorResult);
            if (issue is not null)
            {
                return issue;
            }
        }

        if (request.PhoneNumberChanged)
        {
            var result = await userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
            if (!result.Succeeded)
            {
                return Results.ValidationProblem(result.ToDictionary());
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                if (request.PhoneNumberConfirmed.HasValue)
                {
                    user.PhoneNumberConfirmed = request.PhoneNumberConfirmed.Value;
                }
            }
            else
            {
                user.PhoneNumberConfirmed = false;
            }
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.ValidationProblem(updateResult.ToDictionary());
        }

        var roles = await roleAssignmentService.GetUserRoleNamesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserUpdated,
            user.Id,
            new
            {
                user.Email,
                Roles = roles,
                user.EmailConfirmed,
                user.LockoutEnabled
            },
            cancellationToken).ConfigureAwait(false);

        return await GetUserAsync(id, userManager, roleAssignmentService, cancellationToken);
    }

    private static async Task<IResult> LockUserAsync(
        Guid id,
        AdminUserLockRequest request,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var lockoutEnd = request?.Minutes is > 0
            ? DateTimeOffset.UtcNow.AddMinutes(request!.Minutes.Value)
            : DateTimeOffset.UtcNow.AddDays(DefaultLockoutDays);

        var lockoutEnabledResult = await userManager.SetLockoutEnabledAsync(user, true);
        var issue = EnsureSuccess(lockoutEnabledResult);
        if (issue is not null)
        {
            return issue;
        }

        var lockoutEndResult = await userManager.SetLockoutEndDateAsync(user, lockoutEnd);
        issue = EnsureSuccess(lockoutEndResult);
        if (issue is not null)
        {
            return issue;
        }

        var securityStampResult = await userManager.UpdateSecurityStampAsync(user);
        issue = EnsureSuccess(securityStampResult);
        if (issue is not null)
        {
            return issue;
        }

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserLocked,
            user.Id,
            new { user.Email, lockoutEnd },
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> UnlockUserAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var lockoutResetResult = await userManager.SetLockoutEndDateAsync(user, null);
        var issue = EnsureSuccess(lockoutResetResult);
        if (issue is not null)
        {
            return issue;
        }

        var resetFailedResult = await userManager.ResetAccessFailedCountAsync(user);
        issue = EnsureSuccess(resetFailedResult);
        if (issue is not null)
        {
            return issue;
        }

        var securityStampResult = await userManager.UpdateSecurityStampAsync(user);
        issue = EnsureSuccess(securityStampResult);
        if (issue is not null)
        {
            return issue;
        }

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserUnlocked,
            user.Id,
            new { user.Email },
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> ForcePasswordResetAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IAccountEmailService accountEmailService,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        await accountEmailService.SendPasswordResetEmailAsync(user, cancellationToken).ConfigureAwait(false);

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserPasswordReset,
            user.Id,
            new { user.Email },
            cancellationToken).ConfigureAwait(false);

        return Results.Accepted($"/admin/users/{user.Id:D}/force-password-reset");
    }

    private static async Task<IResult> ResetMfaAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var twoFactorResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        var issue = EnsureSuccess(twoFactorResult);
        if (issue is not null)
        {
            return issue;
        }

        var resetAuthResult = await userManager.ResetAuthenticatorKeyAsync(user);
        issue = EnsureSuccess(resetAuthResult);
        if (issue is not null)
        {
            return issue;
        }

        var securityStampResult = await userManager.UpdateSecurityStampAsync(user);
        issue = EnsureSuccess(securityStampResult);
        if (issue is not null)
        {
            return issue;
        }

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserMfaReset,
            user.Id,
            new { user.Email },
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> ResendConfirmationEmailAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IAccountEmailService accountEmailService,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        if (user.EmailConfirmed)
        {
            return Results.Problem("User email is already confirmed.", statusCode: StatusCodes.Status400BadRequest);
        }

        await accountEmailService.SendConfirmationEmailAsync(user, cancellationToken).ConfigureAwait(false);

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserConfirmationResent,
            user.Id,
            new { user.Email },
            cancellationToken).ConfigureAwait(false);

        return Results.Accepted($"/admin/users/{user.Id:D}/resend-confirmation");
    }

    private static async Task<IResult> GetUserRolesAsync(
        Guid id,
        IRoleAssignmentService roleAssignmentService,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var roles = await roleAssignmentService.GetUserRoleNamesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new AdminUserRolesResponse(roles));
    }

    private static async Task<IResult> UpdateUserRolesAsync(
        Guid id,
        AdminUserRolesUpdateRequest request,
        IRoleAssignmentService roleAssignmentService,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var normalizedRoles = NormalizeRoleNames(request.Roles);
        await roleAssignmentService.AssignRolesAsync(user.Id, normalizedRoles, cancellationToken).ConfigureAwait(false);

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserRolesUpdated,
            user.Id,
            new { user.Email, Roles = normalizedRoles },
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> SoftDeleteUserAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        var lockoutEnabledResult = await userManager.SetLockoutEnabledAsync(user, true);
        var issue = EnsureSuccess(lockoutEnabledResult);
        if (issue is not null)
        {
            return issue;
        }

        var lockoutEndResult = await userManager.SetLockoutEndDateAsync(user, SoftDeleteLockoutEnd);
        issue = EnsureSuccess(lockoutEndResult);
        if (issue is not null)
        {
            return issue;
        }

        var twoFactorResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        issue = EnsureSuccess(twoFactorResult);
        if (issue is not null)
        {
            return issue;
        }
        user.EmailConfirmed = false;
        user.PhoneNumberConfirmed = false;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.ValidationProblem(updateResult.ToDictionary());
        }

        var securityStampResult = await userManager.UpdateSecurityStampAsync(user);
        issue = EnsureSuccess(securityStampResult);
        if (issue is not null)
        {
            return issue;
        }

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserDeleted,
            user.Id,
            new { user.Email },
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> RestoreUserAsync(
        Guid id,
        UserManager<ApplicationUser> userManager,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.NotFound();
        }

        if (!IsSoftDeleted(user))
        {
            return Results.NoContent();
        }

        var lockoutEndResult = await userManager.SetLockoutEndDateAsync(user, null);
        var issue = EnsureSuccess(lockoutEndResult);
        if (issue is not null)
        {
            return issue;
        }

        var lockoutEnabledResult = await userManager.SetLockoutEnabledAsync(user, false);
        issue = EnsureSuccess(lockoutEnabledResult);
        if (issue is not null)
        {
            return issue;
        }

        var securityStampResult = await userManager.UpdateSecurityStampAsync(user);
        issue = EnsureSuccess(securityStampResult);
        if (issue is not null)
        {
            return issue;
        }

        await auditLogger.LogAsync(
            AuditEventTypes.AdminUserRestored,
            user.Id,
            new { user.Email },
            cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static bool TryNormalizeMetadata(
        IDictionary<string, string?>? metadata,
        RegistrationOptions options,
        out Dictionary<string, string?> normalized,
        out Dictionary<string, string[]> errors)
    {
        normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var input = metadata is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(metadata, StringComparer.OrdinalIgnoreCase);

        var knownFields = new HashSet<string>(options.ProfileFields.Select(field => field.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var key in input.Keys)
        {
            if (!knownFields.Contains(key))
            {
                errors[$"metadata.{key}"] = new[] { "Unknown profile field." };
            }
        }

        foreach (var field in options.ProfileFields)
        {
            input.TryGetValue(field.Name, out var rawValue);
            var value = rawValue?.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.Required)
                {
                    errors[$"metadata.{field.Name}"] = new[] { "Field is required." };
                }

                normalized[field.Name] = null;
                continue;
            }

            if (value.Length > field.MaxLength)
            {
                errors[$"metadata.{field.Name}"] = new[] { $"Field exceeds maximum length of {field.MaxLength} characters." };
                continue;
            }

            if (!string.IsNullOrWhiteSpace(field.Pattern) && !Regex.IsMatch(value, field.Pattern))
            {
                errors[$"metadata.{field.Name}"] = new[] { "Field does not match the required pattern." };
                continue;
            }

            normalized[field.Name] = value;
        }

        return errors.Count == 0;
    }

    private static IReadOnlyList<string> NormalizeRoleNames(IEnumerable<string>? roles)
    {
        if (roles is null)
        {
            return Array.Empty<string>();
        }

        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSoftDeleted(ApplicationUser user)
        => user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value == SoftDeleteLockoutEnd;

    private static IResult? EnsureSuccess(IdentityResult result)
        => result.Succeeded ? null : Results.ValidationProblem(result.ToDictionary());
}

internal sealed record AdminUserListQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? Search { get; init; }

    public string? Role { get; init; }

    public bool? Locked { get; init; }
}

internal sealed record AdminUserListResponse(int Page, int PageSize, int TotalCount, IReadOnlyList<AdminUserSummary> Users)
{
    public static AdminUserListResponse Empty(int page, int pageSize)
        => new(page, pageSize, 0, Array.Empty<AdminUserSummary>());
}

internal sealed record AdminUserSummary(
    Guid Id,
    string? Email,
    string? DisplayName,
    bool EmailConfirmed,
    bool IsLockedOut,
    DateTimeOffset CreatedAt,
    bool MfaEnabled,
    IReadOnlyList<string> Roles,
    bool IsDeleted);

internal sealed record AdminUserDetailResponse(
    Guid Id,
    string? Email,
    bool EmailConfirmed,
    string? DisplayName,
    DateTimeOffset CreatedAt,
    bool LockoutEnabled,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    bool TwoFactorEnabled,
    bool PhoneNumberConfirmed,
    string? PhoneNumber,
    IReadOnlyDictionary<string, string?> Metadata,
    string ConcurrencyStamp,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AdminUserExternalLogin> ExternalLogins,
    bool AuthenticatorConfigured,
    bool IsDeleted);

internal sealed record AdminUserExternalLogin(
    string Provider,
    string DisplayName,
    string Key);

internal sealed class AdminUserCreateRequest
{
    public string Email { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string? DisplayName { get; init; }
    public IDictionary<string, string?>? Metadata { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool SendConfirmationEmail { get; init; }
    public bool SendPasswordResetEmail { get; init; }
    public IList<string>? Roles { get; init; }
}

internal sealed class AdminUserUpdateRequest
{
    public string ConcurrencyStamp { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public IDictionary<string, string?>? Metadata { get; init; }
    public bool? EmailConfirmed { get; init; }
    public bool? LockoutEnabled { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public bool? TwoFactorEnabled { get; init; }
    public string? PhoneNumber { get; init; }
    public bool? PhoneNumberConfirmed { get; init; }
    public bool PhoneNumberChanged => PhoneNumber is not null;
}

internal sealed class AdminUserLockRequest
{
    public int? Minutes { get; init; }
}

internal sealed record AdminUserRolesResponse(IReadOnlyList<string> Roles);

internal sealed class AdminUserRolesUpdateRequest
{
    public IList<string>? Roles { get; init; }
}
