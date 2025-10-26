# Identity Base Organizations

`Identity.Base.Organizations` layers organization management on top of the core Identity Base and RBAC packages. It provides EF Core entities, services, hosted infrastructure, and minimal API endpoints so any host can manage organizations, memberships, and organization-scoped roles without custom scaffolding.

## Features
- Organization aggregate (`Organization`, `OrganizationMetadata`) with per-tenant slug/display name uniqueness.
- Membership service with primary-organization tracking, role assignments, and helper queries for listing memberships.
- Organization-specific role catalog and claim formatter that augments Identity Base permission claims with organization context.
- Hosted migration/seed services that keep the organizations schema current and bootstrap default roles (`OrgOwner`, `OrgManager`, `OrgMember`).
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
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);
rolesBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString);
});

var organizationsBuilder = builder.Services.AddIdentityBaseOrganizations(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString);
});

var app = builder.Build();
app.UseApiPipeline();
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganizationEndpoints();
await app.RunAsync();
```

If you omit the options callback, the package attempts to use the `IdentityOrganizations` connection string from configuration.

### 3. Apply migrations
`Identity.Base.Organizations` ships with an initial migration for `OrganizationDbContext`:
```bash
dotnet ef database update \
  --project Identity.Base.Organizations/Identity.Base.Organizations.csproj \
  --context Identity.Base.Organizations.Data.OrganizationDbContext
```

The hosted `OrganizationMigrationHostedService` also applies pending migrations on startup when the provider is relational.

### 4. Seed default roles
`OrganizationRoleSeeder` creates the default system roles. Register additional callbacks if you need to extend the seed pipeline:
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
| `GET /organizations` | List organizations (optionally filter by `tenantId` query). | `organizations.read` |
| `POST /organizations` | Create an organization. | `organizations.manage` |
| `GET /organizations/{id}` | Retrieve one organization. | `organizations.read` |
| `PATCH /organizations/{id}` | Update display name, metadata, or status. | `organizations.manage` |
| `DELETE /organizations/{id}` | Archive an organization. | `organizations.manage` |
| `GET /organizations/{id}/members` | List memberships + role assignments. | `organization.members.read` |
| `POST /organizations/{id}/members` | Add a user to the organization. | `organization.members.manage` |
| `PUT /organizations/{id}/members/{userId}` | Update membership roles/primary flag. | `organization.members.manage` |
| `DELETE /organizations/{id}/members/{userId}` | Remove a membership. | `organization.members.manage` |
| `GET /organizations/{id}/roles` | List organization + shared roles. | `organization.roles.read` |
| `POST /organizations/{id}/roles` | Create a custom organization role. | `organization.roles.manage` |
| `DELETE /organizations/{id}/roles/{roleId}` | Delete a custom role. | `organization.roles.manage` |

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

## Extensibility
```csharp
organizationsBuilder
    .ConfigureOrganizationModel(modelBuilder => { /* custom EF configuration */ })
    .AfterOrganizationSeed(async (sp, ct) => { /* custom seeding */ })
    .AddOrganizationClaimFormatter<CustomFormatter>()
    .AddOrganizationScopeResolver<CustomScopeResolver>();
```

## Testing
Run the solution tests to execute the organizations unit suite alongside the existing Identity Base coverage:
```bash
dotnet test Identity.sln
```

## License
MIT, consistent with the rest of the Identity Base OSS packages.
