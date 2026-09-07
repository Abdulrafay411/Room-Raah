using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class OwnerProfile
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public string CnicNumber { get; set; } = string.Empty;
    public VerificationStatus IdentityStatus { get; set; } = VerificationStatus.NotChecked;
    public DateTime? IdentityCheckedAt { get; set; }
    public int? IdentityCheckedByAdminId { get; set; }
    public string PayoutContact { get; set; } = string.Empty;

    public AppUser AppUser { get; set; } = null!;
}
