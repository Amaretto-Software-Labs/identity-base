namespace Identity.Base.Roles.Entities;

public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public required string Action { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
