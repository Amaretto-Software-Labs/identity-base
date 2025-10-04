namespace Identity.Base.Organizations.Options;

public sealed class OrganizationOptions
{
    public int SlugMaxLength { get; set; } = 128;

    public int DisplayNameMaxLength { get; set; } = 256;

    public int MetadataMaxBytes { get; set; } = 16_384;

    public int MetadataMaxKeyLength { get; set; } = 64;

    public int MetadataMaxValueLength { get; set; } = 512;
}
