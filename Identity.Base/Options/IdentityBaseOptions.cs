using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Base.Options;

public sealed class IdentityBaseOptions
{
    private readonly List<Action<IServiceCollection, IConfiguration>> _optionConfigurators = new();

    public bool UseDefaultOptionBinding { get; set; } = true;

    internal IReadOnlyList<Action<IServiceCollection, IConfiguration>> OptionConfigurators => _optionConfigurators;

    public IdentityBaseOptions ConfigureOptions(Action<IServiceCollection, IConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _optionConfigurators.Add(configure);
        return this;
    }
}
