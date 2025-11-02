# Identity Base Organisations

`Identity.Base.Organisations` layers organisation management on top of the core Identity Base and RBAC packages. It provides EF Core entities, services, hosted infrastructure, and minimal API endpoints so any host can manage organisations, memberships, and organisation-scoped roles without custom scaffolding.

## Features
- Organisation aggregate (`Organisation`, `OrganisationMetadata`) with per-tenant slug/display name uniqueness.
- Membership service with primary-organisation tracking, role assignments, and helper queries for listing memberships.
- Organisation-specific role catalog and claim formatter that augments Identity Base permission claims with organisation context.
- Hosted migration/seed services that keep the organisations schema current and bootstrap default roles (`OrgOwner`, `OrgManager`, `OrgMember`).
- Minimal API modules for CRUD, membership management, role management, and user-facing endpoints.
- Builder hooks (`ConfigureOrganisationModel`, `AfterOrganisationSeed`, `AddOrganisationClaimFormatter`, `AddOrganisationScopeResolver`) mirroring Identity Base extensibility points.

## Installation

### 1. Add the package
```bash
dotnet add package Identity.Base.Organisations
```

### 2. Register services
Add the organisations services after `AddIdentityBase` (and optionally `AddIdentityRoles`) in `Program.cs`:
```csharp
using Identity.Base.Extensions;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityBase(builder.Configuration, builder.Environment);
var rolesBuilder = builder.Services.AddIdentityRoles(builder.Configuration);
rolesBuilder.AddDbContext<IdentityRolesDbContext>((provider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString);
});

var organisationsBuilder = builder.Services.AddIdentityBaseOrganisations(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Primary")!;
    options.UseNpgsql(connectionString);
});

var app = builder.Build();
app.UseApiPipeline(appBuilder => appBuilder.UseSerilogRequestLogging());
app.MapApiEndpoints();
app.MapIdentityRolesUserEndpoints();
app.MapIdentityBaseOrganisationEndpoints();
await app.RunAsync();
```

If you omit the options callback, the package attempts to use the `IdentityOrganisations` connection string from configuration.

### 3. Apply migrations
`Identity.Base.Organisations` ships with an initial migration for `OrganisationDbContext`:
```bash
dotnet ef database update \
  --project Identity.Base.Organisations/Identity.Base.Organisations.csproj \
  --context Identity.Base.Organisations.Data.OrganisationDbContext
```

The hosted `OrganisationMigrationHostedService` also applies pending migrations on startup when the provider is relational.

### 4. Seed default roles
`OrganisationRoleSeeder` creates the default system roles. Register additional callbacks if you need to extend the seed pipeline:
```csharp
organisationsBuilder.AfterOrganisationSeed(async (sp, ct) =>
{
    // e.g. provision billing metadata, assign baseline memberships, etc.
});
```

### 5. Customize the model
Use `ConfigureOrganisationModel` to add indexes or shadow properties:
```csharp
organisationsBuilder.ConfigureOrganisationModel(modelBuilder =>
{
    modelBuilder.Entity<Organisation>().HasIndex(org => org.CreatedAtUtc);
});
```

## API surface

| Method & Route | Description | Permission |
| --- | --- | --- |
| `GET /organisations` | List organisations (optionally filter by `tenantId` query). | `organisations.read` |
| `POST /organisations` | Create an organisation. | `organisations.manage` |
| `GET /organisations/{id}` | Retrieve one organisation. | `organisations.read` |
| `PATCH /organisations/{id}` | Update display name, metadata, or status. | `organisations.manage` |
| `DELETE /organisations/{id}` | Archive an organisation. | `organisations.manage` |
| `GET /organisations/{id}/members` | List memberships + role assignments. | `organisation.members.read` |
| `POST /organisations/{id}/members` | Add a user to the organisation. | `organisation.members.manage` |
| `PUT /organisations/{id}/members/{userId}` | Update membership roles/primary flag. | `organisation.members.manage` |
| `DELETE /organisations/{id}/members/{userId}` | Remove a membership. | `organisation.members.manage` |
| `GET /organisations/{id}/roles` | List organisation + shared roles. | `organisation.roles.read` |
| `POST /organisations/{id}/roles` | Create a custom organisation role. | `organisation.roles.manage` |
| `DELETE /organisations/{id}/roles/{roleId}` | Delete a custom role. | `organisation.roles.manage` |

Authorization is enforced through the Identity Base RBAC package. The default `IOrganisationScopeResolver` verifies the caller is a member of the target organisation; override it (or `IPermissionClaimFormatter`) via the builder extensions to compose tenant-specific or elevated administrator rules.

## Options
- `OrganisationOptions`
  - `SlugMaxLength`, `DisplayNameMaxLength`
  - `MetadataMaxBytes`, `MetadataMaxKeyLength`, `MetadataMaxValueLength`
- `OrganisationRoleOptions`
  - `NameMaxLength`, `DescriptionMaxLength`
  - Default role names (`OwnerRoleName`, `ManagerRoleName`, `MemberRoleName`)

Bind or override using the standard options pattern:
```csharp
builder.Services.Configure<OrganisationOptions>(builder.Configuration.GetSection("Organisations"));
```

## Extensibility
```csharp
organisationsBuilder
    .ConfigureOrganisationModel(modelBuilder => { /* custom EF configuration */ })
    .AfterOrganisationSeed(async (sp, ct) => { /* custom seeding */ })
    .AddOrganisationClaimFormatter<CustomFormatter>()
    .AddOrganisationScopeResolver<CustomScopeResolver>();
```

## Testing
Run the solution tests to execute the organisations unit suite alongside the existing Identity Base coverage:
```bash
dotnet test Identity.sln
```

## License
MIT, consistent with the rest of the Identity Base OSS packages.
