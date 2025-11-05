# Identity.Base.AspNet

## Overview
`Identity.Base.AspNet` is the companion package for APIs and worker services that need to consume Identity Base-issued JWTs. It wraps the standard ASP.NET Core authentication/authorization plumbing with defaults tuned for Identity Base: authority configuration, scope policies, request logging helpers, and convenience extensions for minimal APIs.

Typical scenarios include:
- Protecting REST APIs with scopes exposed by the Identity Base server.
- Enforcing permission-based policies derived from token claims.
- Diagnosing authentication issues locally with detailed request/response logging.

## Installation & Wiring

Install from NuGet:

```bash
dotnet add package Identity.Base.AspNet
```

Configure services and middleware in your API host. `AddIdentityBaseAuthentication` registers both the JWT bearer handler and `AddAuthorization()` so you can immediately use `RequireScope` on endpoints:

```csharp
using Identity.Base.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityBaseAuthentication(
    authority: "https://identity.example.com",
    audience: "identity.api"); // optional, default is identity.api

var app = builder.Build();

app.UseIdentityBaseRequestLogging(enableDetailedLogging: app.Environment.IsDevelopment());
app.UseIdentityBaseAuthentication(); // adds authentication + authorization middlewares

app.MapGet("/secure", () => "OK")
   .RequireAuthorization(policy => policy.RequireScope("identity.api"));

app.Run();
```

`UseIdentityBaseAuthentication` wires `UseAuthentication()` and `UseAuthorization()` in the correct order. The optional request logging middleware traces JWT validation results and scope checks—enable detailed logging only in development due to the verbosity.

## Configuration

`AddIdentityBaseAuthentication` accepts:
- `authority` (required): Base URL of your Identity Base host.
- `audience` (optional): JWT audience; defaults to `identity.api`.
- `configure` callback (optional): modify the underlying `JwtBearerOptions` (for example, to customise events or disable HTTPS metadata checks in testing).

Example `appsettings.json` and wiring:

```json
{
  "IdentityBase": {
    "Authority": "https://identity.example.com",
    "Audience": "identity.api"
  }
}
```

```csharp
var identitySettings = builder.Configuration.GetSection("IdentityBase");
builder.Services.AddIdentityBaseAuthentication(
    authority: identitySettings["Authority"]!,
    audience: identitySettings.GetValue("Audience", "identity.api"));
```

## Public Surface

| API | Description |
| --- | --- |
| `AddIdentityBaseAuthentication(string authority, string? audience = null, Action<JwtBearerOptions>? configure = null)` | Registers JWT bearer authentication tuned for Identity Base token format and adds `AddAuthorization()`. |
| `UseIdentityBaseAuthentication()` | Inserts `UseAuthentication()` and `UseAuthorization()` in the pipeline. |
| `UseIdentityBaseRequestLogging(bool enableDetailedLogging = false)` | Logs request metadata and JWT validation events for troubleshooting. |
| `RequireScope(...)` extension methods | Fluent helpers to require one or more scopes on endpoints. |

## Extension Points
- Provide a custom `JwtBearerEvents` instance via the `configure` callback to audit token validation or propagate headers downstream.
- Override the HTTP back-channel (e.g., for local certificate validation or custom discovery caching).
- Register additional authorization handlers/policies alongside the built-in scope requirement helpers.

## Troubleshooting & Tips
- **401 Unauthorized** – usually indicates an invalid/missing `Authorization: Bearer` header or an audience mismatch. Confirm that the API expects the same `audience` claim that Identity Base issues (`identity.api` by default).
- **Scope failures** – `RequireScope("identity.api")` returns `403` when the JWT lacks the required scope. Ensure the client requested the scope during the authorization code flow, and the API uses the same string.
- **Local HTTPS certificates** – when developing against `https://localhost`, the middleware automatically relaxes certificate validation. For remote test environments, supply a custom `BackchannelHttpHandler` via the `configure` callback.
- **Verbose logging** – `UseIdentityBaseRequestLogging(true)` records token contents and validation steps. Enable only in development to avoid leaking sensitive data in production logs.

## Dependencies & Compatibility
- Targets ASP.NET Core 9 (minimal APIs and MVC controllers).
- Intended for APIs that trust an Identity Base authority hosted separately.
- Compatible with `Identity.Base.Roles`/`Identity.Base.Organizations` tokens that include permission and organization claims.

## Examples & Guides
- Sample usage in `apps/` (e.g., `org-sample-api`).
- [Getting Started Guide](../../guides/getting-started.md#secure-additional-apis) and [Full-stack Integration Guide](../../guides/full-stack-integration-guide.md#protect-additional-microservices).

## Change Log
- See [CHANGELOG.md](../../CHANGELOG.md) (`Identity.Base.AspNet` entries)
