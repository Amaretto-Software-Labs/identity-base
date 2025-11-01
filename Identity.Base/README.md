# Identity.Base

Identity.Base is a reusable Identity + OpenIddict service library for .NET 9.0. It bundles ASP.NET Core Identity, EF Core migrations, OpenIddict server configuration, MFA, external providers, and email integrations into ergonomic extension methods that can be hosted by any ASP.NET Core application.

## Getting Started

```csharp
var builder = WebApplication.CreateBuilder(args);

var identity = builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
identity
    .AddConfiguredExternalProviders() // Google / Microsoft / Apple based on configuration
    .AddExternalAuthProvider("github", auth =>
    {
        // register custom external providers here
        return auth.AddOAuth("GitHub", options => { /* ... */ });
    });

var app = builder.Build();
app.UseApiPipeline(); // Add request logging middleware if desired
app.MapControllers();
app.MapApiEndpoints();
app.Run();
```

By default the Identity Host (or any consumer calling `AddIdentityBase`) applies the packaged EF Core migrations during startup. You only need to generate custom migrations when you extend the provided DbContexts.

See the repository README for the full architecture, configuration schemas, and microservice/React integration guides.

## Documentation
- [Getting Started](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/docs/guides/getting-started.md)
- [Identity.Base Public API](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/docs/reference/identity-base-public-api.md)
- [Release Checklist](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/docs/release/release-checklist.md)

## License
Identity.Base is released under the [MIT License](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/LICENSE).
