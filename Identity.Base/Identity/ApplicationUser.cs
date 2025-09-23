using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 128;

    [MaxLength(DisplayNameMaxLength)]
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public UserProfileMetadata ProfileMetadata { get; private set; } = UserProfileMetadata.Empty;

    public void SetProfileMetadata(UserProfileMetadata metadata)
    {
        ProfileMetadata = metadata;
    }

    public void SetProfileMetadata(IDictionary<string, string?> values)
        => ProfileMetadata = UserProfileMetadata.FromDictionary(values);
}
