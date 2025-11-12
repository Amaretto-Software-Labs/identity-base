namespace Identity.Base.Lifecycle;

public sealed class LifecycleHookOptions
{
    public LifecycleHookFailureBehavior AfterFailureBehavior { get; set; } = LifecycleHookFailureBehavior.LogAndContinue;
}

public enum LifecycleHookFailureBehavior
{
    Bubble = 0,
    LogAndContinue = 1
}
