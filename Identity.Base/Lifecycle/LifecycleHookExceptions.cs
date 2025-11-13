using System;

namespace Identity.Base.Lifecycle;

public sealed class LifecycleHookRejectedException : InvalidOperationException
{
    public LifecycleHookRejectedException(string operation, string? reason = null)
        : base(string.IsNullOrWhiteSpace(reason)
            ? $"Lifecycle hook rejected operation '{operation}'."
            : $"Lifecycle hook rejected operation '{operation}': {reason}")
    {
        Operation = operation;
        Reason = reason;
    }

    public string Operation { get; }

    public string? Reason { get; }
}

public sealed class LifecycleHookExecutionException : InvalidOperationException
{
    public LifecycleHookExecutionException(string operation, Exception innerException)
        : base($"Lifecycle hook failed during '{operation}'.", innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }
}
