using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Identity.Base.Options;
using Identity.Base.Organisations.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrgSampleApi.Sample.Invitations;

namespace OrgSampleApi.Hosting.Endpoints;

internal static class SampleEndpointHelpers
{
    public static async Task<IResult?> EnsureActorInScopeAsync(
        ClaimsPrincipal principal,
        IOrganisationScopeResolver scopeResolver,
        Guid organisationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        if (organisationId == Guid.Empty)
        {
            return Results.BadRequest(new Dictionary<string, string[]> { ["organisationId"] = ["Organisation identifier is required."] });
        }

        var inScope = await scopeResolver.IsInScopeAsync(userId, organisationId, cancellationToken).ConfigureAwait(false);
        if (!inScope)
        {
            return Results.Forbid();
        }

        return null;
    }

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }

    public static (string? RegisterUrl, string? ClaimUrl) ResolveInvitationLinks(InvitationLinkOptions options, Guid code, bool isExistingUser)
    {
        var registerUrl = isExistingUser ? null : FormatUrl(options.RegisterUrlTemplate, code);
        var claimUrl = isExistingUser ? FormatUrl(options.ClaimUrlTemplate, code) : null;

        return (registerUrl, claimUrl);
    }

    public static string? ResolveDisplayName(IDictionary<string, string?> metadata, RegistrationOptions options)
    {
        if (metadata is null || options?.ProfileFields is null)
        {
            return null;
        }

        var preferredField = options.ProfileFields.FirstOrDefault(field => field.Name.Equals("displayName", StringComparison.OrdinalIgnoreCase));
        if (preferredField is not null && metadata.TryGetValue(preferredField.Name, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return null;
    }

    private static string? FormatUrl(string template, Guid code)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        return template.Replace("{code}", code.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
