namespace CivicSignal.Application.Abstractions.Storage;

public interface IFileStorageService
{
    Task<StoredFileInfo> StoreAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storageUri,
        CancellationToken cancellationToken = default);
}
