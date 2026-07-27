using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using CivicSignal.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace CivicSignal.Infrastructure.Storage;

internal sealed class S3FileStorageService(
    IAmazonS3 s3,
    IOptions<S3FileStorageOptions> s3Options,
    IOptions<LocalFileStorageOptions> fileOptions) : IFileStorageService
{
    public async Task<StoredFileInfo> StoreAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var safeFileName = NormalizeFileName(fileName);
        var normalizedContentType = NormalizeContentType(contentType);
        EnsureContentTypeAllowed(normalizedContentType);

        await using var uploadBuffer = new MemoryStream();
        await CopyWithLimitAsync(content, uploadBuffer, fileOptions.Value.MaxUploadBytes, cancellationToken);
        uploadBuffer.Position = 0;

        var objectKey = BuildObjectKey(safeFileName);
        var request = new PutObjectRequest
        {
            BucketName = s3Options.Value.BucketName,
            Key = objectKey,
            InputStream = uploadBuffer,
            ContentType = normalizedContentType
        };

        await s3.PutObjectAsync(request, cancellationToken);

        return new StoredFileInfo(
            safeFileName,
            normalizedContentType,
            BuildStorageUri(objectKey));
    }

    public async Task<Stream?> OpenReadAsync(
        string storageUri,
        CancellationToken cancellationToken = default)
    {
        var objectKey = ResolveObjectKey(storageUri);
        if (objectKey is null)
        {
            return null;
        }

        try
        {
            var response = await s3.GetObjectAsync(
                s3Options.Value.BucketName,
                objectKey,
                cancellationToken);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == HttpStatusCode.NotFound
            || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    private string BuildObjectKey(string safeFileName)
    {
        var prefix = NormalizePrefix(s3Options.Value.Prefix);
        var objectName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{Path.GetExtension(safeFileName)}";

        return string.IsNullOrWhiteSpace(prefix) ? objectName : $"{prefix}/{objectName}";
    }

    private string BuildStorageUri(string objectKey)
    {
        var publicBaseUrl = s3Options.Value.PublicBaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return $"{publicBaseUrl}/{objectKey}";
        }

        return $"s3://{s3Options.Value.BucketName}/{objectKey}";
    }

    private string? ResolveObjectKey(string storageUri)
    {
        if (string.IsNullOrWhiteSpace(storageUri))
        {
            return null;
        }

        var trimmed = storageUri.Trim();
        var s3Prefix = $"s3://{s3Options.Value.BucketName}/";
        if (trimmed.StartsWith(s3Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[s3Prefix.Length..];
        }

        var publicBaseUrl = s3Options.Value.PublicBaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl)
            && trimmed.StartsWith($"{publicBaseUrl}/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[(publicBaseUrl.Length + 1)..];
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var bucketPathPrefix = $"/{s3Options.Value.BucketName}/";
            if (uri.AbsolutePath.StartsWith(bucketPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(uri.AbsolutePath[bucketPathPrefix.Length..]);
            }
        }

        return null;
    }

    private static string NormalizePrefix(string? prefix)
    {
        return string.IsNullOrWhiteSpace(prefix)
            ? string.Empty
            : prefix.Trim().Trim('/');
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
        var allowedContentTypes = fileOptions.Value.AllowedContentTypes
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
