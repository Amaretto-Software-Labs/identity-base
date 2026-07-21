namespace Identity.Base.ServicePrincipals.Api;

public sealed record ServicePrincipalSummary(
    Guid Id,
    string DisplayName,
    string ClientId,
    bool IsDisabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyStamp,
    IReadOnlyList<string> Roles);

public sealed record ServicePrincipalDetail(
    Guid Id,
    string DisplayName,
    string ClientId,
    bool IsDisabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyStamp,
    IReadOnlyList<string> Roles);

public sealed record ServicePrincipalCredentialSummary(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    string? RevokedReason);

public sealed record IssuedServicePrincipalCredential(
    Guid Id,
    string Name,
    string Secret,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record CreateServicePrincipalRequest(string DisplayName);
public sealed record UpdateServicePrincipalRequest(string DisplayName, string ConcurrencyStamp);
public sealed record ServicePrincipalRolesResponse(IReadOnlyList<string> Roles);
public sealed record UpdateServicePrincipalRolesRequest(IReadOnlyList<string>? Roles);
public sealed record IssueServicePrincipalCredentialRequest(string Name, DateTimeOffset? ExpiresAt);
public sealed record RevokeServicePrincipalCredentialRequest(string? Reason);
