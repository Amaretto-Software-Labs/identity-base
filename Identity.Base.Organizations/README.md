# Identity Base Organizations

> For the canonical documentation (installation, endpoints, extension points) see [docs/packages/identity-base-organizations/index.md](../docs/packages/identity-base-organizations/index.md). The README provides a quick-start snapshot.

`Identity.Base.Organizations` layers organization management on top of the core Identity Base and RBAC packages. It provides EF Core entities, services, hosted infrastructure, and minimal API endpoints so any host can manage organizations, memberships, and organization-scoped roles without custom scaffolding.

## Features
- Organization aggregate (`Organization`, `OrganizationMetadata`) with per-tenant slug/display name uniqueness.
- Membership service with primary-organization tracking, role assignments, and helper queries for listing memberships.
- Organization-specific role catalog and claim formatter that augments Identity Base permission claims with organization context.
- Hosted seed services that bootstrap default roles (`OrgOwner`, `OrgManager`, `OrgMember`) once your migrations have been applied.
- Minimal API modules for CRUD, membership management, role management, and user-facing endpoints.
- Builder hooks (`ConfigureOrganizationModel`, `AfterOrganizationSeed`, `AddOrganizationClaimFormatter`, `AddOrganizationScopeResolver`) mirroring Identity Base extensibility points.

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

builder.Services.AddIdentityBase(builder.Configuration, builder.Environment, configureDbContext: configureDbContext);
builder.Services.AddIdentityRoles(builder.Configuration, configureDbContext);
builder.Services.AddIdentityBaseOrganizations(configureDbContext);

var app = builder.Build();
app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());
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
organizationsBuilder.AfterOrganizationSeed(async (sp, ct) =>
{
    // e.g. provision billing metadata, assign baseline memberships, etc.
});
```

### 5. Customize the model
Use `ConfigureOrganizationModel` to add indexes or shadow properties:
```csharp
organizationsBuilder.ConfigureOrganizationModel(modelBuilder =>
{
    modelBuilder.Entity<Organization>().HasIndex(org => org.CreatedAtUtc);
});
```

## API surface

| Method & Route | Description | Permission |
| --- | --- | --- |
| `GET /organizations` | List organizations (optionally filter by `tenantId` query). | `admin.organizations.read` |
| `POST /organizations` | Create an organization. | `admin.organizations.manage` |
| `GET /organizations/{id}` | Retrieve one organization. | `admin.organizations.read` |
| `PATCH /organizations/{id}` | Update display name, metadata, or status. | `admin.organizations.manage` |
| `DELETE /organizations/{id}` | Archive an organization. | `admin.organizations.manage` |
| `GET /organizations/{id}/members` | List memberships + role assignments. | `admin.organizations.members.read` |
| `POST /organizations/{id}/members` | Add a user to the organization. | `admin.organizations.members.manage` |
| `PUT /organizations/{id}/members/{userId}` | Update membership roles/primary flag. | `admin.organizations.members.manage` |
| `DELETE /organizations/{id}/members/{userId}` | Remove a membership. | `admin.organizations.members.manage` |
| `GET /organizations/{id}/roles` | List organization + shared roles. | `admin.organizations.roles.read` |
| `POST /organizations/{id}/roles` | Create a custom organization role. | `admin.organizations.roles.manage` |
| `DELETE /organizations/{id}/roles/{roleId}` | Delete a custom role. | `admin.organizations.roles.manage` |

> Default organization roles (Owner/Manager/Member) currently receive only the user-scoped (`user.organizations.*`) permissions. Create a separate role with `admin.organizations.*` permissions if you need a platform-wide organization administrator.

## Active organization context

Tokens issued by Identity Base now include an `org:memberships` claim listing all organization IDs for the signed-in user. Add the middleware in your pipeline:

```csharp
app.UseOrganizationContextFromHeader();
```

Then send the `X-Organization-Id` header on each request. The middleware validates the caller still belongs to that organization (admins with `admin.organizations.*` bypass the membership check) and loads the organization metadata into `IOrganizationContextAccessor`; it automatically ignores the header on the admin `/organizations` APIs so those remain truly global. If a membership changes (for example, the user loses access to an organization), refresh their tokens so the `org:memberships` claim stays up to date.

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
organizationsBuilder
    .ConfigureOrganizationModel(modelBuilder => { /* custom EF configuration */ })
    .AfterOrganizationSeed(async (sp, ct) => { /* custom seeding */ })
    .AddOrganizationCreationListener<CustomOrganizationCreationListener>()
    .AddOrganizationClaimFormatter<CustomFormatter>()
    .AddOrganizationScopeResolver<CustomScopeResolver>();
```

### Organization creation listeners
- Register one or more `IOrganizationCreationListener` implementations via `AddOrganizationCreationListener<T>()`.
- Each listener runs after `OrganizationService.CreateAsync` persists a new organization, enabling billing, automation, or audit hooks without modifying the core service.

## Testing
Run the solution tests to execute the organizations unit suite alongside the existing Identity Base coverage:
```bash
dotnet test Identity.sln
```

## License
MIT, consistent with the rest of the Identity Base OSS packages.
