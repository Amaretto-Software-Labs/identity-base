using Aspire.Hosting;
var builder = DistributedApplication.CreateBuilder(args);

var sharedConnectionString = builder.Configuration["ConnectionStrings:Primary"]
    ?? "Host=localhost;Port=5432;Database=identity_org_sample;Username=postgres;Password=P@ssword123";

var orgSampleApi = builder.AddProject("org-sample-api", "../org-sample-api/OrgSampleApi.csproj", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", "https://localhost:8182")
    .WithEnvironment("ConnectionStrings__Primary", sharedConnectionString)
    .WithHttpsEndpoint(port: 8182, name:"https", isProxied: false)
    .WithExternalHttpEndpoints();

var sampleApi = builder.AddProject("sample-api", "../sample-api/SampleApi.csproj", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", "https://localhost:8199")
    .WithHttpsEndpoint(port: 8199, name:"https", isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(orgSampleApi);

var orgSampleApp = builder.AddNpmApp("org-sample-client", "../org-sample-client", "dev")
    .WithEnvironment("VITE_API_BASE", "https://localhost:8182")
    .WithEnvironment("VITE_CLIENT_ID", "org-sample-client")
    .WithEnvironment("VITE_AUTHORIZE_REDIRECT", "http://localhost:5173/auth/callback")
    .WithEnvironment("VITE_AUTHORIZE_SCOPE", "openid profile email offline_access identity.api")
    .WithEnvironment("PORT", "5173")
    .WithEnvironment("HOST", "0.0.0.0")
    .WithHttpEndpoint(port: 5173, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(orgSampleApi);

builder.AddNpmApp("sample-client", "../sample-client", "dev")
    .WithEnvironment("VITE_API_BASE", "https://localhost:8181")
    .WithEnvironment("PORT", "5174")
    .WithEnvironment("HOST", "0.0.0.0")
    .WithHttpEndpoint(port: 5174, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(sampleApi)
    .WaitFor(orgSampleApp);

builder.Build().Run();
