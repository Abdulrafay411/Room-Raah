namespace RoomRaah.Application.DTOs;

/// <summary>One photo on a listing.</summary>
public class PropertyPhotoDto
{
    /// <summary>Identifier, used to delete this photo or make it primary.</summary>
    public int Id { get; set; }

    /// <summary>The full-size image, for the gallery.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The generated thumbnail, for cards and lists.</summary>
    public string ThumbnailUrl { get; set; } = string.Empty;

    /// <summary>Whether this is the photo shown on the listing card.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Position in the gallery, starting at 1.</summary>
    public int SortOrder { get; set; }

    /// <summary>Live, PendingAdd or PendingRemove.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the photo was uploaded.</summary>
    public DateTime UploadedAt { get; set; }
}
