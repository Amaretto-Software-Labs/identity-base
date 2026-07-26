namespace Identity.Base.Options;

public sealed class IdentityDbNamingOptions
{
    public const string DefaultTablePrefix = "Identity";

    private string _tablePrefix = DefaultTablePrefix;

    public string TablePrefix
    {
        get => _tablePrefix;
        set => _tablePrefix = Normalize(value);
    }

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? DefaultTablePrefix : value.Trim();
}
