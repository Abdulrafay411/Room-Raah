namespace RoomRaah.Application.Listings;

/// <summary>
/// The photo limits from the product document, bound from configuration so the
/// deployed values and the documented ones are the same values.
/// </summary>
public class PhotoSettings
{
    /// <summary>The blob container photos are stored in.</summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>Largest accepted upload, in bytes.</summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>Photos a listing needs before it can be submitted.</summary>
    public int MinPhotosPerListing { get; set; }

    /// <summary>Photos a listing may hold at once.</summary>
    public int MaxPhotosPerListing { get; set; }

    /// <summary>Long edge of the generated thumbnail, in pixels.</summary>
    public int ThumbnailLongEdgePx { get; set; }

    /// <summary>
    /// The three formats an upload may be in. Not configuration: adding a fourth
    /// means teaching the thumbnail encoder to read it, which no setting can do.
    /// </summary>
    public static readonly string[] AcceptedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];
}
