using Identity.Base.Extensions;
using Identity.Base.Features.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Identity.Base.Email.SendGrid;

/// <summary>
/// Helper extensions for wiring the SendGrid email sender into Identity Base hosts.
/// </summary>
public static class SendGridServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SendGrid templated email sender and supporting options on the supplied service collection.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <param name="configuration">Application configuration source.</param>
    /// <returns>The supplied service collection for method chaining.</returns>
    public static IServiceCollection AddSendGridEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SendGridOptions>()
            .Bind(configuration.GetSection(SendGridOptions.SectionName))
            .ValidateDataAnnotations();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SendGridOptions>, SendGridOptionsValidator>());
        services.AddHttpClient(SendGridEmailSender.HttpClientName, client => client.BaseAddress = new Uri("https://api.sendgrid.com/"));
        services.Replace(ServiceDescriptor.Scoped<ITemplatedEmailSender, SendGridEmailSender>());
        services.AddHealthChecks().AddCheck<SendGridOptionsHealthCheck>("sendgrid");

        return services;
    }

    /// <summary>
    /// Configures the <see cref="IdentityBaseBuilder"/> to send templated emails through SendGrid.
    /// </summary>
    /// <param name="builder">The Identity Base builder.</param>
    /// <returns>The supplied builder for method chaining.</returns>
    public static IdentityBaseBuilder UseSendGridEmailSender(this IdentityBaseBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<SendGridOptions>()
            .Bind(builder.Configuration.GetSection(SendGridOptions.SectionName))
            .ValidateDataAnnotations();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SendGridOptions>, SendGridOptionsValidator>());
        builder.Services.AddHttpClient(SendGridEmailSender.HttpClientName, client => client.BaseAddress = new Uri("https://api.sendgrid.com/"));
        builder.Services.Replace(ServiceDescriptor.Scoped<ITemplatedEmailSender, SendGridEmailSender>());
        builder.Services.AddHealthChecks().AddCheck<SendGridOptionsHealthCheck>("sendgrid");

        return builder;
    }
}
