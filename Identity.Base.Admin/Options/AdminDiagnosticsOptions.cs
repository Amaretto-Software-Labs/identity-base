using System;

namespace Identity.Base.Admin.Options;

public sealed class AdminDiagnosticsOptions
{
    public const string SectionName = "Identity:Admin:Diagnostics";

    /// <summary>
    /// Duration threshold that triggers a warning-level log entry for admin query executions.
    /// Defaults to 500 milliseconds.
    /// </summary>
    public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromMilliseconds(500);
}
