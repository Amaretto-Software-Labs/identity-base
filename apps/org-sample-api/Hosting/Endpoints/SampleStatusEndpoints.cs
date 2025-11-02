using System;
using System.Linq;
using Identity.Base.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrgSampleApi.Sample;

namespace OrgSampleApi.Hosting.Endpoints;

internal static class SampleStatusEndpoints
{
    public static RouteGroupBuilder MapSampleStatusEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/status", () => Results.Ok(new
        {
            Message = "Organization sample API is running.",
            Timestamp = DateTimeOffset.UtcNow
        }));

        group.MapGet("/defaults", (IOptions<OrganizationBootstrapOptions> options) =>
        {
            var defaults = options.Value;
            return Results.Ok(new
            {
                defaults.Slug,
                defaults.DisplayName,
                defaults.Metadata
            });
        });

        group.MapGet("/registration/profile-fields", (IOptions<RegistrationOptions> options) =>
        {
            var registration = options.Value;
            var fields = registration.ProfileFields.Select(field => new
            {
                field.Name,
                field.DisplayName,
                field.Required,
                field.MaxLength,
                field.Pattern
            });

            return Results.Ok(new { fields });
        });

        return group;
    }
}
