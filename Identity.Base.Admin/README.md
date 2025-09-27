# Identity.Base.Admin

Administrative extensions for Identity Base. Includes:

- Authorization helpers enforcing admin scope + permission claims
- Minimal API endpoints for `/admin/users` (listing and detail)
- Integrations with `Identity.Base.Roles` for assigning roles and auditing actions

Consumers opt in by referencing this package and invoking the provided service/endpoint extensions. No admin schema or endpoints are added unless explicitly enabled.

## Usage

```csharp
// Configure services
var adminBuilder = services.AddIdentityAdmin(configuration);

// Configure RBAC storage (choose one)
adminBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
{
    var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>();
    options.UseNpgsql(databaseOptions.Value.Primary);
});
// or adminBuilder.UseDbContext<MyAppDbContext>();

// During startup seed RBAC data
await app.Services.SeedIdentityRolesAsync();

// Map the admin endpoints alongside the core Identity Base surfaces
app.MapApiEndpoints();
app.MapIdentityAdminEndpoints();
```

Additional admin capabilities (mutations, audits, role management) will arrive in subsequent iterations.
