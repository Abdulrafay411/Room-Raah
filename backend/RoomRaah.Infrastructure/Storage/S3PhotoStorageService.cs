using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;

namespace RoomRaah.Infrastructure.Storage;

/// <summary>
/// The same photographs, on S3.
///
/// §8 of the product document put <c>IDistanceCalculator</c> behind an interface so a
/// paid provider could be swapped in later without touching a handler; storage was
/// given the same seam for the same reason, and this is it being used. Everything
/// about deciding whether an upload is an image, and about generating the thumbnail,
/// is in <see cref="PhotoImaging"/> and shared with the Azure implementation — the
/// only thing that differs between the two is where the bytes go and what URL comes
/// back.
///
/// The object keys are identical across providers on purpose, so a deployment can move
/// between them and keep §9's documented layout.
/// </summary>
public class S3PhotoStorageService : IPhotoStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly PhotoSettings _photos;
    private readonly S3StorageSettings _storage;

    public S3PhotoStorageService(
        IAmazonS3 s3,
        IOptions<PhotoSettings> photos,
        IOptions<S3StorageSettings> storage)
    {
        _s3 = s3;
        _photos = photos.Value;
        _storage = storage.Value;
    }

    public async Task<StoredPhoto?> UploadAsync(int propertyId, Stream content, string fileName)
    {
        // Read once into a byte array rather than rewinding one stream between the
        // passes, for the reason the Azure implementation gives: SKCodec takes
        // ownership of the stream it is handed and closes it.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        var format = PhotoImaging.DetectFormat(bytes);
        if (format is null)
        {
            return null;
        }

        var thumbnail = PhotoImaging.CreateThumbnail(bytes, _photos.ThumbnailLongEdgePx);
        if (thumbnail is null)
        {
            return null;
        }

        var (originalKey, thumbnailKey) = PhotoImaging.NamesFor(
            propertyId, Guid.NewGuid().ToString("N"), fileName);

        using var originalContent = new MemoryStream(bytes);
        await PutAsync(originalKey, originalContent, PhotoImaging.AcceptedFormats[format.Value]);

        using (thumbnail)
        {
            await PutAsync(thumbnailKey, thumbnail, "image/jpeg");
        }

        return new StoredPhoto(UrlFor(originalKey), UrlFor(thumbnailKey));
    }

    public async Task DeleteAsync(string blobUrl, string thumbnailUrl)
    {
        foreach (var url in new[] { blobUrl, thumbnailUrl })
        {
            var key = KeyFor(url);
            if (key is null)
            {
                continue;
            }

            try
            {
                await _s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _storage.BucketName,
                    Key = key
                });
            }
            catch (Exception)
            {
                // The same decision the Azure implementation makes: an object that
                // will not delete must not fail the request that owns it. The row is
                // already gone, and a file nobody can reach is a storage bill rather
                // than a broken page.
            }
        }
    }

    private Task PutAsync(string key, Stream content, string contentType)
    {
        return _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _storage.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType

            // No per-object ACL. The bucket is expected to be readable through its own
            // policy or a CDN in front of it, and buckets created since 2023 have
            // object ACLs disabled by default — asking for one on such a bucket fails
            // the upload rather than being quietly ignored.
        });
    }

    /// <summary>
    /// The URL the product hands out. A configured base wins, so the bytes can move
    /// behind a CDN without rewriting a single stored row.
    /// </summary>
    private string UrlFor(string key)
    {
        if (!string.IsNullOrWhiteSpace(_storage.PublicBaseUrl))
        {
            return $"{_storage.PublicBaseUrl!.TrimEnd('/')}/{key}";
        }

        return $"{ServiceRoot().TrimEnd('/')}/{_storage.BucketName}/{key}";
    }

    private string ServiceRoot()
    {
        if (!string.IsNullOrWhiteSpace(_storage.ServiceUrl))
        {
            return _storage.ServiceUrl!;
        }

        var region = _s3.Config.RegionEndpoint?.SystemName ?? "us-east-1";

        return $"https://s3.{region}.amazonaws.com";
    }

    /// <summary>
    /// A stored URL back to the key that produced it.
    ///
    /// Both shapes have to work, because a deployment that turns
    /// <c>PublicBaseUrl</c> on later still has rows written before it did — and a
    /// delete that silently matched nothing would leave the file behind for ever.
    /// </summary>
    private string? KeyFor(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_storage.PublicBaseUrl))
        {
            var prefix = _storage.PublicBaseUrl!.TrimEnd('/') + "/";

            if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Decode(url[prefix.Length..]);
            }
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // Path style: .../{bucket}/{key}
        var marker = $"/{_storage.BucketName}/";
        var index = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (index >= 0)
        {
            return Decode(uri.AbsolutePath[(index + marker.Length)..]);
        }

        // Virtual-host style: https://{bucket}.s3.../{key}
        if (uri.Host.StartsWith(_storage.BucketName + ".", StringComparison.OrdinalIgnoreCase))
        {
            return Decode(uri.AbsolutePath.TrimStart('/'));
        }

        return null;
    }

    private static string? Decode(string key)
    {
        var decoded = Uri.UnescapeDataString(key);

        return string.IsNullOrEmpty(decoded) ? null : decoded;
    }
}
