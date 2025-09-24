using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Identity.Base.Options;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "CorsPolicy";

    [MinLength(1)]
    public List<string> AllowedOrigins { get; set; } = new();
}
