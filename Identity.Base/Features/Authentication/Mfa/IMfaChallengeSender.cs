using Identity.Base.Identity;

namespace Identity.Base.Features.Authentication.Mfa;

public interface IMfaChallengeSender
{
    string Method { get; }

    Task SendChallengeAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default);
}

internal sealed class DisabledMfaChallengeSender : IMfaChallengeSender
{
    private readonly string _method;
    private readonly string _message;

    public DisabledMfaChallengeSender(string method, string message)
    {
        _method = method;
        _message = message;
    }

    public string Method => _method;

    public Task SendChallengeAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(_message);
}
