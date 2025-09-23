using System.Collections.Immutable;
using System.Text.Json;

namespace Identity.Base.Identity;

public sealed class UserProfileMetadata
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false
    };

    private readonly ImmutableDictionary<string, string?> _values;

    private UserProfileMetadata(ImmutableDictionary<string, string?> values)
    {
        _values = values;
    }

    public static UserProfileMetadata Empty { get; } = new(ImmutableDictionary<string, string?>.Empty);

    public IReadOnlyDictionary<string, string?> Values => _values;

    public string? this[string key] => _values.TryGetValue(key, out var value) ? value : null;

    public static UserProfileMetadata FromDictionary(IDictionary<string, string?> values)
        => new(values.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));

    public static UserProfileMetadata FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, SerializerOptions) ?? new Dictionary<string, string?>();
        return FromDictionary(dictionary);
    }

    public string ToJson() => JsonSerializer.Serialize(_values, SerializerOptions);
}
