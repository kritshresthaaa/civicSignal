namespace CivicSignal.Infrastructure.Storage;

internal sealed class S3FileStorageOptions
{
    public const string SectionName = "S3Storage";

    public string Endpoint { get; set; } = "http://localhost:9000";

    public string AccessKey { get; set; } = "minioadmin";

    public string SecretKey { get; set; } = "minioadmin";

    public string BucketName { get; set; } = "civic-signal";

    public string Region { get; set; } = "us-east-1";

    public string Prefix { get; set; } = "incident-media";

    public string PublicBaseUrl { get; set; } = "http://localhost:9000/civic-signal";

    public bool ForcePathStyle { get; set; } = true;
}
