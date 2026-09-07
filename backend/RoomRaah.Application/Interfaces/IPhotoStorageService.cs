namespace RoomRaah.Application.Interfaces;

/// <summary>Stores a listing photo and the thumbnail generated from it.</summary>
public interface IPhotoStorageService
{
    /// <summary>
    /// Stores the original and a thumbnail generated from it, and returns both URLs.
    /// Returns null when the bytes are not a readable image in an accepted format:
    /// the thumbnail has to be decoded from them anyway, so the decode is the check.
    /// Which status that turns into is the handler's decision, not this one's.
    /// </summary>
    Task<StoredPhoto?> UploadAsync(int propertyId, Stream content, string fileName);

    /// <summary>Removes an original and its thumbnail. Missing blobs are not an error.</summary>
    Task DeleteAsync(string blobUrl, string thumbnailUrl);
}

/// <summary>Where a stored photo and its thumbnail ended up.</summary>
public record StoredPhoto(string BlobUrl, string ThumbnailUrl);
