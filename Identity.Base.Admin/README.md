# Identity.Base.Admin

> Refer to [docs/packages/identity-base-admin/index.md](../docs/packages/identity-base-admin/index.md) for the canonical documentation covering endpoints, configuration, and extension points.

Administrative extensions for Identity Base. Includes:

- Authorization helpers enforcing admin scope + permission claims
- Minimal API endpoints for `/admin/users` (listing and detail)
- Integrations with `Identity.Base.Roles` for assigning roles and auditing actions

Consumers opt in by referencing this package and invoking the provided service/endpoint extensions. No admin schema or endpoints are added unless explicitly enabled.

## Usage

```csharp
// Configure services & register the RBAC DbContext
var adminBuilder = services.AddIdentityAdmin(
    configuration,
    configureDbContext: (provider, options) =>
    {
        var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Primary")
            ?? throw new InvalidOperationException("ConnectionStrings:Primary must be set.");

        options.UseNpgsql(connectionString); // or UseSqlServer(connectionString)
    });
adminBuilder.UseTablePrefix("Contoso"); // optional

// Alternatively, call adminBuilder.UseDbContext<MyAppDbContext>() if you already registered one.

// During startup seed RBAC data
await app.Services.SeedIdentityRolesAsync();

// Map the admin endpoints alongside the core Identity Base surfaces
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();
```

Additional admin capabilities (mutations, audits, role management) will arrive in subsequent iterations.
