using System.Diagnostics.CodeAnalysis;

namespace Identity.Base.Lifecycle;

public readonly record struct LifecycleHookResult
{
    private LifecycleHookResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        ErrorMessage = error;
    }

    public bool Succeeded { get; }

    public string? ErrorMessage { get; }

    public static LifecycleHookResult Continue() => new(true, null);

    public static LifecycleHookResult Fail(string? error = null) => new(false, error);

    [MemberNotNullWhen(true, nameof(ErrorMessage))]
    public bool IsFailure => !Succeeded;
}
