namespace ReqNest.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "Storage";

    public string? ConnectionString { get; init; }

    public Uri? ServiceUri { get; init; }

    public string DefaultContainer { get; init; } = "requirements";

    public bool MarkDevelopmentUploadsClean { get; init; }
}
