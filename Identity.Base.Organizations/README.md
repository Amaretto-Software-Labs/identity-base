# Identity Base Organizations

> For the canonical documentation (installation, endpoints, extension points) see [docs/packages/identity-base-organizations/index.md](../docs/packages/identity-base-organizations/index.md). The README provides a quick-start snapshot.

`Identity.Base.Organizations` layers organization management on top of the core Identity Base and RBAC packages. It provides EF Core entities, services, hosted infrastructure, and minimal API endpoints so any host can manage organizations, memberships, and organization-scoped roles without custom scaffolding.

## Features
- Organization aggregate (`Organization`, `OrganizationMetadata`) with per-tenant slug/display name uniqueness.
- Membership service with role assignments and paged user/admin queries; active organization selection is explicit and client-side.
- Organization-specific role catalog and claim formatter that augments Identity Base permission claims with organization context.
- Hosted seed services that bootstrap default roles (`OrgOwner`, `OrgManager`, `OrgMember`) once your migrations have been applied.
- Minimal API modules for CRUD, membership management, role management, and user-facing endpoints.
- Core-builder hooks for model/seed/claim/scope customization plus an organizations-builder lifecycle-listener registration.

## Installation

### 1. Add the package
```bash
dotnet add package Identity.Base.Organizations
```

### 2. Register services
Add the organizations services after `AddIdentityBase` (and optionally `AddIdentityRoles`) in `Program.cs`:
```csharp
using Identity.Base.Extensions;
using Identity.Base.Organizations.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext = (sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Primary")
        ?? throw new InvalidOperationException("ConnectionStrings:Primary must be set.");

    options.UseNpgsql(connectionString); // or UseSqlServer(connectionString)
};

var identityBuilder = builder.Services.AddIdentityBase(
    builder.Configuration,
    builder.Environment,
    configureDbContext: configureDbContext);
builder.Services.AddIdentityRoles(builder.Configuration, configureDbContext);
var organizationsBuilder = builder.Services.AddIdentityBaseOrganizations(configureDbContext);

var app = builder.Build();
app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());
app.UseOrganizationContextFromHeader();
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganizationEndpoints();
await app.RunAsync();
```

`AddIdentityBaseOrganizations` no longer auto-configures DbContexts. Provide the delegate shown above or register `OrganizationDbContext` yourself before calling the extension.

### 3. Apply migrations
Generate and apply migrations from your host project targeting the provider you selected:
```bash
dotnet ef migrations add InitialOrganizations --context OrganizationDbContext
dotnet ef database update --context OrganizationDbContext
```

### 4. Seed default roles
`OrganizationRoleSeeder` creates the default system roles after your host has applied migrations. Register additional callbacks if you need to extend the seed pipeline:
```csharp
identityBuilder.AfterOrganizationSeed(async (sp, ct) =>
{
    // e.g. provision billing metadata, assign baseline memberships, etc.
});
```

### 5. Customize the model
Use `ConfigureOrganizationModel` to add indexes or shadow properties:
```csharp
identityBuilder.ConfigureOrganizationModel(modelBuilder =>
{
    modelBuilder.Entity<Organization>().HasIndex(org => org.CreatedAtUtc);
});
```

## API surface

| Method & Route | Description | Permission |
| --- | --- | --- |
| `/admin/organizations` | Platform administration: paged organization CRUD plus nested members, roles, permissions, and invitations. | `admin.organizations.*` |
| `/users/me/organizations` | Self-service organization creation/list/detail plus nested member, role, permission, and invitation management. | Authenticated plus `user.organizations.*` |
| `GET /invitations/{code}` | Return a public invitation preview without invitee email or role IDs. | Anonymous |
| `POST /invitations/claim` | Accept an invitation for the authenticated matching user. | Authenticated |

> Default organization roles (Owner/Manager/Member) currently receive only the user-scoped (`user.organizations.*`) permissions. Create a separate role with `admin.organizations.*` permissions if you need a platform-wide organization administrator.

## Active organization context

Tokens issued by Identity Base now include an `org:memberships` claim listing all organization IDs for the signed-in user. Add the middleware in your pipeline:

```csharp
app.UseOrganizationContextFromHeader();
```

Then send the `X-Organization-Id` header on scoped requests. The middleware validates the caller still belongs to that organization and loads the metadata into `IOrganizationContextAccessor`; admin `/admin/organizations` routes intentionally ignore the header and remain global. If membership changes, refresh tokens so `org:memberships` and permission claims stay up to date.

Authorization is enforced through the Identity Base RBAC package. The default `IOrganizationScopeResolver` verifies the caller is a member of the target organization; override it (or `IPermissionClaimFormatter`) via the builder extensions to compose tenant-specific or elevated administrator rules.

## Options
- `OrganizationOptions`
  - `SlugMaxLength`, `DisplayNameMaxLength`
  - `MetadataMaxBytes`, `MetadataMaxKeyLength`, `MetadataMaxValueLength`
- `OrganizationRoleOptions`
  - `NameMaxLength`, `DescriptionMaxLength`
  - Default role names (`OwnerRoleName`, `ManagerRoleName`, `MemberRoleName`)

Bind or override using the standard options pattern:
```csharp
builder.Services.Configure<OrganizationOptions>(builder.Configuration.GetSection("Organizations"));
```

### Configuration notes
- Auto-binding: `AddIdentityBaseOrganizations` binds options by default
  - `Organizations` → `OrganizationOptions`
  - `Organizations:RoleOptions` → `OrganizationRoleOptions`
  - `Organizations:Authorization` → `OrganizationAuthorizationOptions`
- Role definition overrides: defaults are merged with config; definitions are de-duplicated by name (case-insensitive) and the last entry wins. This lets you override built-in `OrgOwner`/`OrgManager`/`OrgMember` definitions without producing duplicate roles.

## Extensibility
```csharp
identityBuilder
    .ConfigureOrganizationModel(modelBuilder => { /* custom EF configuration */ })
    .AfterOrganizationSeed(async (sp, ct) => { /* custom seeding */ })
    .AddOrganizationClaimFormatter<CustomFormatter>()
    .AddOrganizationScopeResolver<CustomScopeResolver>();

organizationsBuilder
    .AddOrganizationLifecycleListener<CustomOrganizationLifecycleListener>();
```

`IOrganizationLifecycleListener` covers create/update/archive/restore, invitation create/revoke/accept, and membership add/update/remove operations. Legacy creation/update/archive listener helpers remain compatibility shims but are obsolete for new integrations.

## Testing
Run the solution tests to execute the organizations unit suite alongside the existing Identity Base coverage:
```bash
dotnet test Identity.sln
```

## License
MIT, consistent with the rest of the Identity Base OSS packages.
