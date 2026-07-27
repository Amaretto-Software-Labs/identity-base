using Identity.Base.Options;

namespace Identity.Base.Identity;

internal static class IdentityDisplayNameResolver
{
    private static readonly string[] PreferredFieldNames = ["displayName", "fullName"];

    public static string? Resolve(
        IReadOnlyDictionary<string, string?> metadata,
        IEnumerable<RegistrationProfileFieldOptions> configuredFields)
    {
        foreach (var preferredName in PreferredFieldNames)
        {
            var configuredField = configuredFields.FirstOrDefault(field =>
                field.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));
            if (configuredField is null ||
                !metadata.TryGetValue(configuredField.Name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            return value.Trim();
        }

        return null;
    }
}
