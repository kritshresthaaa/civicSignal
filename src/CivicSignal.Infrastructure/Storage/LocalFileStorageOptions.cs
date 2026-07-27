namespace CivicSignal.Infrastructure.Storage;

internal sealed class LocalFileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Provider { get; set; } = "Local";

    public string RootPath { get; set; } = "../../var/uploads/incident-media";

    public string PublicBasePath { get; set; } = "/media";

    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic",
        "image/heif",
        "video/mp4",
        "video/quicktime",
        "audio/mpeg",
        "audio/mp4",
        "audio/ogg",
        "audio/wav",
        "audio/webm",
        "application/pdf"
    ];
}
