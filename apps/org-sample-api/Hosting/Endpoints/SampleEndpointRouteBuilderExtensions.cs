using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace OrgSampleApi.Hosting.Endpoints;

internal static class SampleEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOrgSampleEndpoints(this IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var sampleGroup = builder.MapGroup("/sample").WithTags("Sample");

        sampleGroup.MapSampleStatusEndpoints();
        sampleGroup.MapSampleRegistrationEndpoints();
        sampleGroup.MapSampleMemberEndpoints();
        sampleGroup.MapSampleInvitationEndpoints();

        return builder;
    }
}
