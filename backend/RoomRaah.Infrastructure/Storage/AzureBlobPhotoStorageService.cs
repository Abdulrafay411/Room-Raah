using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;

namespace RoomRaah.Infrastructure.Storage;

/// <summary>
/// Stores the original photo and a thumbnail generated from it. The thumbnail is
/// not a nicety: a search page shows a dozen cards, and a dozen full-size
/// photographs is a page that never finishes loading on mobile data.
/// </summary>
public class AzureBlobPhotoStorageService : IPhotoStorageService
{
    private readonly BlobContainerClient _container;
    private readonly PhotoSettings _settings;

    public AzureBlobPhotoStorageService(BlobServiceClient blobService, IOptions<PhotoSettings> settings)
    {
        _settings = settings.Value;
        _container = blobService.GetBlobContainerClient(_settings.ContainerName);
    }

    public async Task<StoredPhoto?> UploadAsync(int propertyId, Stream content, string fileName)
    {
        // Read once into a byte array rather than rewinding one stream between the
        // three passes. SKCodec.Create takes ownership of the stream it is given and
        // closes it when the codec is disposed, so the second pass over a shared
        // stream reads from a closed one.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        var format = PhotoImaging.DetectFormat(bytes);
        if (format is null)
        {
            return null;
        }

        var thumbnail = PhotoImaging.CreateThumbnail(bytes, _settings.ThumbnailLongEdgePx);
        if (thumbnail is null)
        {
            return null;
        }

        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var (originalName, thumbnailName) = PhotoImaging.NamesFor(
            propertyId, Guid.NewGuid().ToString("N"), fileName);

        using var originalContent = new MemoryStream(bytes);
        var original = await StoreAsync(
            originalName, originalContent, PhotoImaging.AcceptedFormats[format.Value]);

        using (thumbnail)
        {
            var thumb = await StoreAsync(thumbnailName, thumbnail, "image/jpeg");
            return new StoredPhoto(original, thumb);
        }
    }

    public async Task DeleteAsync(string blobUrl, string thumbnailUrl)
    {
        foreach (var url in new[] { blobUrl, thumbnailUrl })
        {
            var client = ClientFor(url);
            if (client is null)
            {
                continue;
            }

            try
            {
                await client.DeleteIfExistsAsync();
            }
            catch (Exception)
            {
                // A blob that will not delete must not fail the request that owns
                // it: the row is already gone, and a file nobody can reach is a
                // storage bill rather than a broken page.
            }
        }
    }

    private async Task<string> StoreAsync(string blobName, Stream content, string contentType)
    {
        var blob = _container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        });

        return blob.Uri.ToString();
    }

    private BlobClient? ClientFor(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var marker = $"/{_settings.ContainerName}/";
        var index = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return null;
        }

        var blobName = Uri.UnescapeDataString(uri.AbsolutePath[(index + marker.Length)..]);

        return string.IsNullOrEmpty(blobName) ? null : _container.GetBlobClient(blobName);
    }
}
