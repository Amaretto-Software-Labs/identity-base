# Identity.Base.AspNet

Easy JWT Bearer authentication integration for ASP.NET Core APIs using Identity.Base.

## Overview

Identity.Base.AspNet simplifies JWT Bearer authentication setup for ASP.NET Core APIs that need to authenticate with Identity.Base tokens. It provides pre-configured extension methods, middleware, and authorization policies to get you up and running quickly.

## Features

- 🔐 **JWT Bearer Authentication** - Pre-configured for Identity.Base tokens
- 🛡️ **Scope-based Authorization** - Built-in support for JWT scope claims
- 🔍 **Request/Response Logging** - Debug authentication flows easily
- ⚙️ **Flexible Configuration** - Customize JWT options as needed
- 🚀 **Development-friendly** - SSL certificate bypass for localhost
- 📋 **Multiple Scope Formats** - Supports various JWT scope claim patterns

## Quick Start

### 1. Install the Package

```bash
dotnet add package Identity.Base.AspNet
```

### 2. Configure Services

In your `Program.cs`:

```csharp
using Identity.Base.AspNet;

var builder = WebApplication.CreateBuilder(args);

// Add Identity.Base JWT authentication
builder.Services.AddIdentityBaseAuthentication("https://your-identity-base-url");

var app = builder.Build();
```

### 3. Configure Middleware

```csharp
// Add request logging (optional, useful for debugging)
app.UseIdentityBaseRequestLogging(enableDetailedLogging: true);

// Add authentication and authorization
app.UseIdentityBaseAuthentication();
```

### 4. Protect Your Endpoints

```csharp
// Basic authentication required
app.MapGet("/api/protected/data", () => "Protected data")
    .RequireAuthorization();

// Require specific scope
app.MapGet("/api/admin", () => "Admin data")
    .RequireAuthorization(policy => policy.RequireScope("identity.api"));
```

## Complete Example

Here's a complete minimal API setup:

```csharp
using Identity.Base.AspNet;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add services
builder.Services.AddOpenApi();

// Configure CORS (adjust origins as needed)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://your-frontend-url")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add Identity.Base JWT authentication
builder.Services.AddIdentityBaseAuthentication("https://your-identity-base-url");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

// Add Identity.Base middleware
app.UseIdentityBaseRequestLogging(enableDetailedLogging: true);
app.UseIdentityBaseAuthentication();

// Public endpoint
app.MapGet("/api/public/status", () => new {
    Status = "OK",
    Message = "API is running",
    Timestamp = DateTime.UtcNow
});

// Protected endpoint
app.MapGet("/api/protected/data", (ClaimsPrincipal user) => new {
    Message = "You are authenticated!",
    User = user.Identity?.Name,
    Claims = user.Claims.Select(c => new { c.Type, c.Value }).ToList()
})
.RequireAuthorization();

// Admin endpoint with scope requirement
app.MapGet("/api/admin", (ClaimsPrincipal user) => new {
    Message = "Admin access granted",
    User = user.Identity?.Name
})
.RequireAuthorization(policy => policy.RequireScope("identity.api"));

app.Run();
```

## API Reference

### Extension Methods

#### `AddIdentityBaseAuthentication`

Configures JWT Bearer authentication for Identity.Base.

```csharp
builder.Services.AddIdentityBaseAuthentication(
    authority: "https://your-identity-base-url",
    audience: "identity.api", // optional, defaults to "identity.api"
    configure: options => {   // optional additional configuration
        // Custom JWT Bearer options
    }
);
```

**Parameters:**
- `authority` (required): Your Identity.Base server URL
- `audience` (optional): JWT audience claim to validate (default: "identity.api")
- `configure` (optional): Additional JWT Bearer configuration callback

#### `UseIdentityBaseRequestLogging`

Adds request/response logging middleware for debugging authentication flows.

```csharp
app.UseIdentityBaseRequestLogging(
    enableDetailedLogging: false // optional, defaults to false for security
);
```

**Parameters:**
- `enableDetailedLogging` (optional): When `true`, shows partial JWT tokens in logs. When `false`, shows "[REDACTED]" for security.

#### `UseIdentityBaseAuthentication`

Adds authentication and authorization middleware to the pipeline.

```csharp
app.UseIdentityBaseAuthentication();
```

This is equivalent to:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

### Authorization Extensions

#### `RequireScope`

Creates authorization policies that require specific JWT scopes.

```csharp
// On endpoints
app.MapGet("/api/admin", handler)
    .RequireAuthorization(policy => policy.RequireScope("identity.api"));

// Multiple scopes
app.MapGet("/api/super-admin", handler)
    .RequireAuthorization(policy =>
        policy.RequireScope("identity.api")
              .RequireScope("admin.write"));
```

#### `HasScope`

Extension method on `ClaimsPrincipal` to check for scopes programmatically.

```csharp
app.MapGet("/api/conditional", (ClaimsPrincipal user) => {
    if (user.HasScope("identity.api"))
    {
        return "You have the required scope";
    }
    return "Insufficient permissions";
});
```

## Scope Formats Supported

The package automatically handles multiple JWT scope claim formats:

1. **Space-separated in single claim**: `"scope": "identity.api admin.read"`
2. **Multiple scope claims**: Multiple `"scope"` claims with individual values
3. **SCP claim format**: `"scp": "identity.api admin.read"` (common in some JWT implementations)

## Configuration Options

### Custom JWT Bearer Configuration

```csharp
builder.Services.AddIdentityBaseAuthentication(
    authority: "https://your-identity-base-url",
    configure: options => {
        options.RequireHttpsMetadata = true; // Enable for production
        options.SaveToken = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5);

        // Custom event handlers
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context => {
                // Custom error handling
                return Task.CompletedTask;
            }
        };
    }
);
```

### Environment-Specific Settings

```csharp
var authority = builder.Environment.IsDevelopment()
    ? "https://localhost:5000"  // Development Identity.Base
    : "https://identity.yourdomain.com";  // Production Identity.Base

builder.Services.AddIdentityBaseAuthentication(authority);
```

## Security Considerations

### Production Checklist

- ✅ Use HTTPS for your Identity.Base authority URL
- ✅ Set `enableDetailedLogging: false` in production (default)
- ✅ Configure CORS origins appropriately
- ✅ Validate JWT audience claims match your API
- ✅ Use proper scope-based authorization for sensitive endpoints

### Development vs Production

The package automatically detects localhost authorities and:
- Disables HTTPS metadata requirements for localhost
- Bypasses SSL certificate validation for localhost
- Enables detailed logging when requested

For production, ensure your Identity.Base server has valid SSL certificates.

## Troubleshooting

### Common Issues

**401 Unauthorized on all protected endpoints**
- Verify your Identity.Base authority URL is correct
- Check that your frontend is sending the JWT token in the `Authorization: Bearer <token>` header
- Enable detailed logging to see authentication failures

**Token validation fails**
- Ensure your Identity.Base server is running and accessible
- Verify the JWT audience matches your configuration
- Check that the JWT hasn't expired

**Scope authorization fails**
- Verify your Identity.Base server includes the expected scopes in JWT tokens
- Use the `HasScope()` extension method to debug scope claims
- Check the JWT token payload for scope claim format

### Debug Logging

Enable detailed request logging:

```csharp
// In Program.cs
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// In middleware
app.UseIdentityBaseRequestLogging(enableDetailedLogging: true);
```

This will log:
- Incoming requests with authentication headers (redacted by default)
- JWT token validation results
- User claims after successful authentication
- Authorization failures with reasons

## Migration from Manual Setup

If you're currently using manual JWT Bearer configuration, here's how to migrate:

### Before (Manual Setup)
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://your-identity-base-url";
        options.Audience = "identity.api";
        // ... many lines of configuration
    });

builder.Services.AddAuthorization();

// ... manual middleware setup
// ... manual scope checking logic
```

### After (Identity.Base.AspNet)
```csharp
builder.Services.AddIdentityBaseAuthentication("https://your-identity-base-url");

// Later in pipeline
app.UseIdentityBaseRequestLogging();
app.UseIdentityBaseAuthentication();

// Scope checking
.RequireAuthorization(policy => policy.RequireScope("identity.api"))
```

## Requirements

- .NET 9.0 or later
- ASP.NET Core
- Identity.Base server

## License

Distributed under the [MIT License](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/LICENSE).

## Contributing

Please review the repository [Contributing Guide](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/CONTRIBUTING.md) and [Code of Conduct](https://github.com/Amaretto-Software-Labs/identity-base/blob/main/CODE_OF_CONDUCT.md) before opening issues or pull requests.
