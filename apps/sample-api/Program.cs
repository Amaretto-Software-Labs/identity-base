using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add services to the container.
builder.Services.AddOpenApi();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Configure JWT Bearer authentication with JWKS endpoint
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:5000";
        options.Audience = "identity.api";
        options.RequireHttpsMetadata = false; // For development only

        // Configure HTTP client to bypass SSL validation
        options.BackchannelHttpHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
        };

        // Enable detailed logging for debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError("Authentication failed: {Error}", context.Exception?.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Token validated successfully for user: {User}",
                    context.Principal?.Identity?.Name ?? "Unknown");

                // Log all claims for debugging
                var claims = context.Principal?.Claims?.ToList() ?? new List<System.Security.Claims.Claim>();
                foreach (var claim in claims)
                {
                    logger.LogDebug("Claim: {Type} = {Value}", claim.Type, claim.Value);
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

// Add request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
    var authHeaderDisplay = "None";
    if (authHeader != null && authHeader.StartsWith("Bearer "))
    {
        var token = authHeader.Substring("Bearer ".Length);
        authHeaderDisplay = $"Bearer {token[..Math.Min(20, token.Length)]}...";
    }

    logger.LogInformation("Request: {Method} {Path} - Auth Header: {AuthHeader}",
        context.Request.Method,
        context.Request.Path,
        authHeaderDisplay);

    await next();

    logger.LogInformation("Response: {StatusCode} - User authenticated: {IsAuthenticated} - User: {User}",
        context.Response.StatusCode,
        context.User?.Identity?.IsAuthenticated ?? false,
        context.User?.Identity?.Name ?? "None");
});

app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Public endpoint - no authentication required
app.MapGet("/api/public/status", () => new {
    Status = "OK",
    Message = "Sample API is running",
    Timestamp = DateTime.UtcNow
})
.WithName("GetPublicStatus");

// Protected endpoint - requires authentication
app.MapGet("/api/protected/weather", (ClaimsPrincipal user) =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return new { Data = forecast, User = GetUserInfo(user) };
})
.RequireAuthorization()
.WithName("GetProtectedWeather");

// Protected endpoint - user profile
app.MapGet("/api/protected/profile", (ClaimsPrincipal user) =>
{
    return GetUserInfo(user);
})
.RequireAuthorization()
.WithName("GetUserProfile");

// Protected endpoint - requires specific scope
app.MapGet("/api/protected/admin", (ClaimsPrincipal user) =>
{
    return new {
        Message = "Admin access granted",
        User = GetUserInfo(user)
    };
})
.RequireAuthorization(policy =>
{
    policy.RequireAssertion(context =>
    {
        // Check for scope claim in various formats
        var user = context.User;

        // Option 1: Check for "scope" claim with space-separated values
        var scopeClaim = user.FindFirst("scope")?.Value;
        if (!string.IsNullOrEmpty(scopeClaim) && scopeClaim.Split(' ').Contains("identity.api"))
        {
            return true;
        }

        // Option 2: Check for multiple "scope" claims
        var scopes = user.FindAll("scope").Select(c => c.Value);
        if (scopes.Contains("identity.api"))
        {
            return true;
        }

        // Option 3: Check for "scp" claim (common in some JWT implementations)
        var scpClaim = user.FindFirst("scp")?.Value;
        if (!string.IsNullOrEmpty(scpClaim) && scpClaim.Split(' ').Contains("identity.api"))
        {
            return true;
        }

        return false;
    });
})
.WithName("GetAdminData");

static object GetUserInfo(ClaimsPrincipal user)
{
    return new
    {
        IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
        Name = user.Identity?.Name,
        UserId = user.FindFirst("sub")?.Value,
        Email = user.FindFirst("email")?.Value,
        Claims = user.Claims.Select(c => new { c.Type, c.Value }).ToList()
    };
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
