# Identity.Base

Identity.Base is a reusable Identity + OpenIddict service library for .NET 9.0. It bundles ASP.NET Core Identity, OpenIddict server configuration, MFA, external providers, and optional email integrations into ergonomic extension methods that can be hosted by any ASP.NET Core application.

## Getting Started

```csharp
var builder = WebApplication.CreateBuilder(args);

var identity = builder.Services.AddIdentityBase(
    builder.Configuration,
    builder.Environment,
    configureDbContext: (sp, options) =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("Primary")
            ?? throw new InvalidOperationException("ConnectionStrings:Primary is required.");

        options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure());
        // or options.UseSqlServer(connectionString);
    });
identity
    .UseTablePrefix("Contoso")          // optional: overrides default "Identity_" prefix
    .AddConfiguredExternalProviders() // Google / Microsoft / Apple based on configuration
    .AddExternalAuthProvider("github", auth =>
    {
        // register custom external providers here
        return auth.AddOAuth("GitHub", options => { /* ... */ });
    })
    .UseMailJetEmailSender();          // optional add-on package

var app = builder.Build();
app.UseApiPipeline(); // Add request logging middleware if desired
app.MapControllers();
app.MapApiEndpoints();
app.Run();
```

Identity.Base no longer ships EF Core migrations. Hosts are responsible for generating and applying migrations (typically from their web/API project) for whichever database provider they choose. Call `dotnet ef migrations add InitialIdentityBase` from your host, run the migrations, then start the application. Call `UseTablePrefix` if you need the tables to be emitted with a prefix other than the default `Identity_`. Enable Mailjet delivery by referencing `Identity.Base.Email.MailJet` and calling `UseMailJetEmailSender()`.

See the repository README for the full architecture, configuration schemas, and microservice/React integration guides.

## Documentation
- [Getting Started](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/docs/guides/getting-started.md)
- [Identity.Base Public API](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/docs/reference/identity-base-public-api.md)
- [Release Checklist](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/docs/release/release-checklist.md)

## License
Identity.Base is released under the [MIT License](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/LICENSE).
