using Identity.Base.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Identity.Base.Tests;

public sealed class PasskeyOptionsValidatorTests
{
    [Fact]
    public void Validate_AcceptsBothSignupModesWithExactDevelopmentOrigins()
    {
        var validator = CreateValidator(["http://localhost:5173"]);
        var options = ValidOptions();

        validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RejectsPasskeyOriginMissingFromCors()
    {
        var validator = CreateValidator(["http://localhost:5174"]);
        var options = ValidOptions();

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Cors:AllowedOrigins");
    }

    [Fact]
    public void Validate_RejectsEnabledSignupModesWhenFeatureIsDisabled()
    {
        var validator = CreateValidator(["http://localhost:5173"]);
        var options = ValidOptions();
        options.Enabled = false;

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("must be empty");
    }

    private static PasskeyOptionsValidator CreateValidator(IReadOnlyList<string> corsOrigins)
    {
        var values = corsOrigins
            .Select((origin, index) => KeyValuePair.Create<string, string?>(
                $"Cors:AllowedOrigins:{index}",
                origin));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new PasskeyOptionsValidator(configuration, new TestHostEnvironment());
    }

    private static PasskeyOptions ValidOptions() => new()
    {
        Enabled = true,
        ServerDomain = "localhost",
        AllowedOrigins = ["http://localhost:5173"],
        Signup = new PasskeySignupOptions
        {
            EnabledModes = [PasskeySignupModes.Assisted, PasskeySignupModes.Passwordless],
            ConfirmationUrlTemplate = "http://localhost:5173/register/passkey?draftId={draftId}&token={token}",
            DraftLifetimeMinutes = 30
        },
        Recovery = new PasskeyRecoveryOptions
        {
            ConfirmationUrlTemplate = "http://localhost:5173/recover/passkey?draftId={draftId}&token={token}",
            DraftLifetimeMinutes = 30
        }
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Identity.Base.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
