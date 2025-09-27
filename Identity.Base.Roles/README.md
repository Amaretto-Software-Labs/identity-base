# Identity.Base.Roles

Role-based access control primitives for Identity Base. Includes:

- EF Core entities and configuration for roles, permissions, role-permission associations, user-role links, and audit entries.
- Configuration binding for role/permission definitions and default role assignments.
- Services for role assignment and permission resolution.
- Optional DbContext (`IdentityRolesDbContext`) and design-time factory.

## Usage

```csharp
// Register RBAC services
var rolesBuilder = services.AddIdentityRoles(configuration);

// If using the provided context
rolesBuilder.AddDbContext<IdentityRolesDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Or map an existing DbContext that implements IRoleDbContext
rolesBuilder.UseDbContext<AppDbContext>();

// Map endpoints when building the app
app.MapIdentityRolesUserEndpoints();

// During startup
await app.Services.SeedIdentityRolesAsync();
```

Consumers opt in explicitly; no additional tables are created unless the package is referenced and configured.
