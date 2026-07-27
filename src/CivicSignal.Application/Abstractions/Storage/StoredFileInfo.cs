namespace CivicSignal.Application.Abstractions.Storage;

public sealed record StoredFileInfo(
    string FileName,
    string ContentType,
    string StorageUri);
