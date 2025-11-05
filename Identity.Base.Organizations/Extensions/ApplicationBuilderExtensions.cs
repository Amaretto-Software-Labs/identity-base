using System;
using Identity.Base.Organizations.Infrastructure;
using Microsoft.AspNetCore.Builder;

namespace Identity.Base.Organizations.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseOrganizationContextFromHeader(this IApplicationBuilder builder, string headerName = OrganizationContextHeaderNames.OrganizationId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseMiddleware<OrganizationContextFromHeaderMiddleware>(headerName);
    }
}
