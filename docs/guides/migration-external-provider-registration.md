# Migration: External Provider Registration Is Now Host-Driven

This release removes built-in social provider wiring from `Identity.Base` and makes external auth fully provider-agnostic.

## Breaking Changes

- Removed fluent helpers:
  - `AddConfiguredExternalProviders()`
  - `AddGoogleAuth(...)`
  - `AddMicrosoftAuth(...)`
  - `AddAppleAuth(...)`
- `ExternalProviders` appsettings binding is no longer used by `AddIdentityBase`.
- `/healthz` no longer emits the `externalProviders` check from core.

## What Still Works

- External auth endpoints remain:
  - `GET /auth/external/{provider}/start`
  - `GET /auth/external/{provider}/callback`
  - `DELETE /auth/external/{provider}`
- Identity Base still handles:
  - external sign-in callback processing
  - account linking/unlinking
  - user association/creation during external login

## Required Host Changes

Register each provider scheme explicitly in your Identity Host and map the route key with `AddExternalAuthProvider(...)`.

```csharp
identityBuilder.AddExternalAuthProvider(
    provider: "github",   // used in /auth/external/{provider}/...
    scheme: "GitHub",     // ASP.NET authentication scheme name
    addScheme: auth => auth.AddOAuth("GitHub", options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"]!;
        options.CallbackPath = "/signin-github";
        options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        options.TokenEndpoint = "https://github.com/login/oauth/access_token";
        options.UserInformationEndpoint = "https://api.github.com/user";
        return;
    }));
```

Notes:
- `provider` is the URL segment clients call.
- `scheme` must match the authentication scheme registered with ASP.NET Core.
- If you keep scheme and provider identical (for example both `github`), you can still map explicitly for clarity.

## Client Impact

Client libraries do not register providers. They only call the configured route key:
- `buildExternalStartUrl("github", "login", returnUrl)`
- `unlinkExternalProvider("github")`

If the host has not registered that provider key/scheme mapping, Identity Base returns:
- `400 Unknown external provider '{provider}'`.
