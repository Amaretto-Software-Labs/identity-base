using FluentValidation;
using Identity.Base.Extensions;
using Identity.Base.Identity;
using Identity.Base.Logging;
using Identity.Base.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Base.Features.Users;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/users")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = IdentityConstants.ApplicationScheme });

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the current user's profile and metadata.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithTags("Users");

        group.MapPut("/me/profile", UpdateProfileAsync)
            .WithName("UpdateUserProfile")
            .WithSummary("Updates the current user's profile metadata.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Users");

        return group;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new UserProfileResponse(
            user.Id,
            user.Email,
            user.EmailConfirmed,
            user.DisplayName,
            user.ProfileMetadata.Values,
            user.ConcurrencyStamp ?? string.Empty,
            user.TwoFactorEnabled));
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpContext context,
        UpdateProfileRequest request,
        IValidator<UpdateProfileRequest> validator,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<RegistrationOptions> registrationOptions,
        IAuditLogger auditLogger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!string.Equals(user.ConcurrencyStamp, request.ConcurrencyStamp, StringComparison.Ordinal))
        {
            return Results.Problem("Profile was updated by another process.", statusCode: StatusCodes.Status409Conflict);
        }

        var metadata = request.Metadata ?? new Dictionary<string, string?>();
        var fields = registrationOptions.Value.ProfileFields;
        var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            metadata.TryGetValue(field.Name, out var rawValue);
            var value = rawValue?.Trim();
            normalized[field.Name] = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        user.SetProfileMetadata(normalized);

        if (normalized.TryGetValue("displayName", out var displayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName.Trim();
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.ValidationProblem(updateResult.ToDictionary());
        }

        await signInManager.RefreshSignInAsync(user);

        await auditLogger.LogAsync(AuditEventTypes.ProfileUpdated, user.Id, normalized, cancellationToken);

        return Results.Ok(new UserProfileResponse(
            user.Id,
            user.Email,
            user.EmailConfirmed,
            user.DisplayName,
            user.ProfileMetadata.Values,
            user.ConcurrencyStamp ?? string.Empty,
            user.TwoFactorEnabled));
    }
}

internal sealed record UserProfileResponse(
    Guid Id,
    string? Email,
    bool EmailConfirmed,
    string? DisplayName,
    IReadOnlyDictionary<string, string?> Metadata,
    string ConcurrencyStamp,
    bool TwoFactorEnabled);

internal sealed class UpdateProfileRequest
{
    public IDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public string ConcurrencyStamp { get; init; } = string.Empty;
}

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator(IOptions<RegistrationOptions> registrationOptions)
    {
        RuleFor(x => x.ConcurrencyStamp)
            .NotEmpty();

        RuleFor(x => x.Metadata)
            .NotNull();

        RuleFor(x => x)
            .Custom((request, context) =>
            {
                var options = registrationOptions.Value;
                var fields = options.ProfileFields;
                var metadata = request.Metadata ?? new Dictionary<string, string?>();
                var knownFields = new HashSet<string>(fields.Select(field => field.Name), StringComparer.OrdinalIgnoreCase);

                foreach (var key in metadata.Keys)
                {
                    if (!knownFields.Contains(key))
                    {
                        context.AddFailure($"metadata.{key}", "Unknown profile field.");
                    }
                }

                foreach (var field in fields)
                {
                    metadata.TryGetValue(field.Name, out var rawValue);
                    var value = rawValue?.Trim();

                    if (field.Required && string.IsNullOrWhiteSpace(value))
                    {
                        context.AddFailure($"metadata.{field.Name}", "Field is required.");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(value))
                    {
                        if (value.Length > field.MaxLength)
                        {
                            context.AddFailure($"metadata.{field.Name}", $"Field exceeds maximum length of {field.MaxLength} characters.");
                        }

                        if (!string.IsNullOrWhiteSpace(field.Pattern) && !Regex.IsMatch(value, field.Pattern))
                        {
                            context.AddFailure($"metadata.{field.Name}", "Field does not match the required pattern.");
                        }
                    }
                }
            });
    }
}
