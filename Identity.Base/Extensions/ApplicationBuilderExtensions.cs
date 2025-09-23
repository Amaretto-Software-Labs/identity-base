using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Identity.Base.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseSerilogRequestLogging();

        return app;
    }
}
