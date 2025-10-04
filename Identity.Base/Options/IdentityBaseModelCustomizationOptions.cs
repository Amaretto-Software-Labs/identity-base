using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Options;

/// <summary>
/// Holds callbacks that can customize Entity Framework models at application start.
/// </summary>
public sealed class IdentityBaseModelCustomizationOptions
{
    private readonly List<Action<ModelBuilder>> _appDbContextCustomizations = new();
    private readonly List<Action<ModelBuilder>> _identityRolesDbContextCustomizations = new();
    private readonly List<Action<ModelBuilder>> _organizationDbContextCustomizations = new();

    public IReadOnlyList<Action<ModelBuilder>> AppDbContextCustomizations => _appDbContextCustomizations;

    public IReadOnlyList<Action<ModelBuilder>> IdentityRolesDbContextCustomizations => _identityRolesDbContextCustomizations;

    public IReadOnlyList<Action<ModelBuilder>> OrganizationDbContextCustomizations => _organizationDbContextCustomizations;

    internal void AddAppDbContextCustomization(Action<ModelBuilder> customization)
    {
        ArgumentNullException.ThrowIfNull(customization);
        _appDbContextCustomizations.Add(customization);
    }

    internal void AddIdentityRolesDbContextCustomization(Action<ModelBuilder> customization)
    {
        ArgumentNullException.ThrowIfNull(customization);
        _identityRolesDbContextCustomizations.Add(customization);
    }

    internal void AddOrganizationDbContextCustomization(Action<ModelBuilder> customization)
    {
        ArgumentNullException.ThrowIfNull(customization);
        _organizationDbContextCustomizations.Add(customization);
    }
}
