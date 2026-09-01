namespace Identity.Base.Features.Authentication.Passkeys;

internal sealed class PasskeyRegistrationDraft
{
    public Guid Id { get; set; }
    public Guid ReservedUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ProfileMetadataJson { get; set; } = "{}";
    public string? DisplayName { get; set; }
    public byte[] ConfirmationTokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? EmailConfirmedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}
