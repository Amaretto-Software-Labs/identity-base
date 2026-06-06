# Identity Permission Guards

Permission guards let you declare authorization rules right where you map endpoints, without hand-parsing claims or re-implementing policy logic. In Identity Base, guards are small helpers that turn a permission string into a full authorization requirement, so your API surface stays readable and consistent.

## Objective

Make permission checks explicit and repeatable. Instead of copying the same claim and scope checks across endpoints, you express the required permission once and let the framework enforce it everywhere.

## What the guards do

Identity Base exposes two guard helpers:

- `RequireAdminPermission("...")` for admin endpoints.
- `RequireOrganizationPermission("...")` for organization endpoints.

Under the hood they evaluate the `identity.permissions` claim issued by `Identity.Base.Roles`. The admin guard also honors the optional `IdentityAdmin:RequiredScope` setting, while the organization guard enforces the organization admin scope when you use `admin.organizations.*` permissions. The result is a single, consistent decision path for authorization instead of scattered checks.

## Usage

Register the relevant packages and then apply the guard to your endpoints. Your permissions still come from your RBAC configuration and role assignments, but the guard is the piece that enforces them at the edge.

```csharp
using Identity.Base;
using Identity.Base.Admin;
using Identity.Base.Organizations;
using Identity.Base.Roles;

builder.Services.AddIdentityBase(builder.Configuration, configureDbContext);
builder.Services.AddIdentityRoles(builder.Configuration, configureDbContext);

builder.Services.AddIdentityAdmin(builder.Configuration, configureDbContext);
builder.Services.AddIdentityBaseOrganizations(builder.Configuration, configureDbContext);
```

## Example: Guarding custom endpoints

```csharp
using Identity.Base.Admin;
using Identity.Base.Organizations;

app.MapGet("/admin/audit", () => Results.Ok("ok"))
    .RequireAuthorization(policy => policy.RequireAdminPermission("users.read"));

app.MapPost("/organizations/{organizationId:guid}/sync", (Guid organizationId) => Results.Ok())
    .RequireAuthorization(policy => policy.RequireOrganizationPermission("admin.organizations.manage"));
```

## Example: Guarding a user-scoped organization route

```csharp
app.MapGet("/users/me/organizations/{organizationId:guid}/roles", () => Results.Ok())
    .RequireAuthorization(policy => policy.RequireOrganizationPermission("user.organizations.roles.read"));
```

If you want to avoid string literals, use the constants in `Identity.Base.Organizations.Authorization` such as `UserOrganizationPermissions.OrganizationRolesRead`.

## When you need custom logic

Sometimes you need conditional logic inside a handler or endpoint. You can still use the claim helpers and keep the same semantics. In addition to `HasPermission`, there are `HasAnyPermission` and `HasAllPermissions` helpers for multi-permission checks.

```csharp
using Identity.Base.Roles.Claims;

app.MapGet("/reports", (ClaimsPrincipal user) =>
{
    if (!user.HasPermission("users.read"))
    {
        return Results.Forbid();
    }

    if (!user.HasAnyPermission(new[] { "users.read", "roles.read" }))
    {
        return Results.Forbid();
    }

    if (!user.HasAllPermissions(new[] { "users.read", "users.manage-roles" }))
    {
        return Results.Forbid();
    }

    return Results.Ok();
});
```

## Migration tips

If you already have manual permission checks, replace them with the guard helpers first. It keeps endpoint mapping readable and moves the policy logic into one tested path. If you see missing permissions, confirm that `Identity.Base.Roles` is registered and that the user has the expected roles so the `identity.permissions` claim is issued.
