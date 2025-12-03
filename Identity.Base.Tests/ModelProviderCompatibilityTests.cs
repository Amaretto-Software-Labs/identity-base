using System;
using Identity.Base.Data;
using Identity.Base.Organizations.Data;
using Identity.Base.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Identity.Base.Tests;

public class ModelProviderCompatibilityTests
{
    public static TheoryData<string, Action<DbContextOptionsBuilder>> Providers => new()
    {
        {
            "SqlServer",
            builder => builder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=IdentityBase_ModelTests;Trusted_Connection=True;Encrypt=False")
        },
        {
            "PostgreSQL",
            builder => builder.UseNpgsql("Host=localhost;Database=identitybase_modeltests;Username=test;Password=test")
        }
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public void AppDbContext_is_compatible_with_provider(string providerName, Action<DbContextOptionsBuilder> configure)
        => AssertProviderCompatibility<AppDbContext>(providerName, configure);

    [Theory]
    [MemberData(nameof(Providers))]
    public void IdentityRolesDbContext_is_compatible_with_provider(string providerName, Action<DbContextOptionsBuilder> configure)
        => AssertProviderCompatibility<IdentityRolesDbContext>(providerName, configure);

    [Theory]
    [MemberData(nameof(Providers))]
    public void OrganizationDbContext_is_compatible_with_provider(string providerName, Action<DbContextOptionsBuilder> configure)
        => AssertProviderCompatibility<OrganizationDbContext>(providerName, configure);

    private static void AssertProviderCompatibility<TContext>(
        string providerName,
        Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        configure(optionsBuilder);

        using var context = (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;

        var model = context.Model;
        Assert.NotNull(model);

        var script = context.Database.GenerateCreateScript();
        Assert.False(string.IsNullOrWhiteSpace(script));

        AssertNoProviderArtifacts(model, providerName);
    }

    private static void AssertNoProviderArtifacts(IModel model, string providerName)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var columnType = property.GetColumnType();
                Assert.False(Contains(columnType, "jsonb"), BuildMessage(providerName, entityType, property, "jsonb in column type"));

                var defaultSql = property.GetDefaultValueSql();
                Assert.False(Contains(defaultSql, "jsonb"), BuildMessage(providerName, entityType, property, "jsonb in default SQL"));
                Assert.False(Contains(defaultSql, "uuid_generate"), BuildMessage(providerName, entityType, property, "uuid_generate in default SQL"));
            }
        }
    }

    private static bool Contains(string? value, string token)
        => value?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string BuildMessage(string providerName, IEntityType entity, IProperty property, string reason)
        => $"Provider {providerName}: {entity.Name}.{property.Name} has provider-specific artifact ({reason}).";
}
