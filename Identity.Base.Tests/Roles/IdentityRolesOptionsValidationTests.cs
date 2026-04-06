using System.Collections.Generic;
using Identity.Base.Roles.Configuration;
using Identity.Base.Roles.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Identity.Base.Tests.Roles;

public sealed class IdentityRolesOptionsValidationTests
{
    [Fact]
    public void AddIdentityRoles_AllowsLegacyPermissionDefinitions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Permissions:Definitions:0:Name"] = "Reports.Read"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddIdentityRoles(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PermissionCatalogOptions>>().Value;
        options.Definitions.ShouldContain(definition => definition.Name == "Reports.Read");
    }

    [Fact]
    public void AddIdentityRoles_AllowsLegacyRolePermissionReferences()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Permissions:Definitions:0:Name"] = "users.read",
                ["Roles:Definitions:0:Name"] = "IdentityAdmin",
                ["Roles:Definitions:0:Permissions:0"] = "Users.Read"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddIdentityRoles(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RoleConfigurationOptions>>().Value;
        options.Definitions.ShouldContain(definition =>
            definition.Name == "IdentityAdmin" &&
            definition.Permissions.Contains("Users.Read"));
    }
}
