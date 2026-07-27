using CivicSignal.Application.Abstractions.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Storage;

internal sealed class LocalFileStorageService(
    IOptions<LocalFileStorageOptions> options,
    IHostEnvironment environment) : IFileStorageService
{
    private const string LocalScheme = "local://incident-media/";

    public async Task<StoredFileInfo> StoreAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var safeFileName = NormalizeFileName(fileName);
        var normalizedContentType = NormalizeContentType(contentType);
        EnsureContentTypeAllowed(normalizedContentType);

        Directory.CreateDirectory(GetRootPath());

        var storedFileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{Path.GetExtension(safeFileName)}";
        var destinationPath = Path.Combine(GetRootPath(), storedFileName);

        await using var destination = File.Create(destinationPath);
        await CopyWithLimitAsync(content, destination, options.Value.MaxUploadBytes, cancellationToken);

        return new StoredFileInfo(
            safeFileName,
            normalizedContentType,
            $"{NormalizePublicBasePath()}/{storedFileName}");
    }

    public Task<Stream?> OpenReadAsync(
        string storageUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageUri))
        {
            return Task.FromResult<Stream?>(null);
        }

        var fileName = ResolveStoredFileName(storageUri.Trim());
        if (fileName is null)
        {
            return Task.FromResult<Stream?>(null);
        }

        var path = Path.Combine(GetRootPath(), fileName);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = File.OpenRead(path);
        return Task.FromResult<Stream?>(stream);
    }

    private string? ResolveStoredFileName(string storageUri)
    {
        if (storageUri.StartsWith(LocalScheme, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(storageUri[LocalScheme.Length..]);
        }

        var publicBasePath = NormalizePublicBasePath();
        if (storageUri.StartsWith($"{publicBasePath}/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(storageUri[(publicBasePath.Length + 1)..]);
        }

        return null;
    }

    private string GetRootPath()
    {
        var configuredRoot = string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? "../../var/uploads/incident-media"
            : options.Value.RootPath.Trim();

        return Path.IsPathRooted(configuredRoot)
            ? Path.GetFullPath(configuredRoot)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredRoot));
    }

    private string NormalizePublicBasePath()
    {
        var publicBasePath = string.IsNullOrWhiteSpace(options.Value.PublicBasePath)
            ? "/media"
            : options.Value.PublicBasePath.Trim();

        return publicBasePath.StartsWith('/') ? publicBasePath.TrimEnd('/') : $"/{publicBasePath.TrimEnd('/')}";
    }

    private static string NormalizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        var safeFileName = Path.GetFileName(fileName.Trim());
        if (safeFileName.Length > 260)
        {
            throw new ArgumentException("File name cannot exceed 260 characters.", nameof(fileName));
        }

        return safeFileName;
    }

    private static string NormalizeContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        return contentType.Trim().ToLowerInvariant();
    }

    private void EnsureContentTypeAllowed(string contentType)
    {
        var allowedContentTypes = options.Value.AllowedContentTypes
            .Select(type => type.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowedContentTypes.Count == 0 || allowedContentTypes.Contains(contentType))
        {
            return;
        }

        throw new InvalidOperationException($"Content type '{contentType}' is not allowed.");
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > maxBytes)
            {
                throw new InvalidOperationException($"File cannot exceed {maxBytes} bytes.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }
}
