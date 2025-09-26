using System.Security.Claims;
using Identity.Base.AspNet;

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

// Configure JWT Bearer authentication using Identity.Base.AspNet
builder.Services.AddIdentityBaseAuthentication("https://localhost:5000");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

// Add Identity.Base logging and authentication middleware
app.UseIdentityBaseRequestLogging(enableDetailedLogging: true);
app.UseIdentityBaseAuthentication();

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
.RequireAuthorization(policy => policy.RequireScope("identity.api"))
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
