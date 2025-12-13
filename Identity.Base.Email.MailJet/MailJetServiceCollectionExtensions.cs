using Identity.Base.Extensions;
using Identity.Base.Features.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.MailJet;

/// <summary>
/// Helper extensions for wiring the Mailjet email sender into Identity Base hosts.
/// </summary>
public static class MailJetServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Mailjet templated email sender and supporting options on the supplied service collection.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <param name="configuration">Application configuration source.</param>
    /// <returns>The supplied service collection for method chaining.</returns>
    public static IServiceCollection AddMailJetEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MailJetOptions>()
            .Bind(configuration.GetSection(MailJetOptions.SectionName))
            .ValidateDataAnnotations();

        services.TryAddSingleton<IValidateOptions<MailJetOptions>, MailJetOptionsValidator>();
        services.AddHttpClient(MailJetEmailSender.HttpClientName, client => client.BaseAddress = new Uri("https://api.mailjet.com/"));
        services.Replace(ServiceDescriptor.Scoped<ITemplatedEmailSender, MailJetEmailSender>());
        services.AddHealthChecks().AddCheck<MailJetOptionsHealthCheck>("mailjet");

        return services;
    }

    /// <summary>
    /// Configures the <see cref="IdentityBaseBuilder"/> to send templated emails through Mailjet.
    /// </summary>
    /// <param name="builder">The Identity Base builder.</param>
    /// <returns>The supplied builder for method chaining.</returns>
    public static IdentityBaseBuilder UseMailJetEmailSender(this IdentityBaseBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<MailJetOptions>()
            .Bind(builder.Configuration.GetSection(MailJetOptions.SectionName))
            .ValidateDataAnnotations();

        builder.Services.TryAddSingleton<IValidateOptions<MailJetOptions>, MailJetOptionsValidator>();
        builder.Services.AddHttpClient(MailJetEmailSender.HttpClientName, client => client.BaseAddress = new Uri("https://api.mailjet.com/"));
        builder.Services.Replace(ServiceDescriptor.Scoped<ITemplatedEmailSender, MailJetEmailSender>());
        builder.Services.AddHealthChecks().AddCheck<MailJetOptionsHealthCheck>("mailjet");

        return builder;
    }
}
