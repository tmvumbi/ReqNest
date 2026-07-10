namespace ReqNest.Core.Storage;

public sealed record StoredBlob(
    string ContainerName,
    string BlobName,
    Uri Uri,
    string ContentType);
