using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class PropertyPhoto
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public PhotoStatus Status { get; set; } = PhotoStatus.Live;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
}
