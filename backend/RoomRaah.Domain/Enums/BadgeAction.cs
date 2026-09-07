namespace RoomRaah.Domain.Enums;

/// <summary>
/// The two things that can happen to an inspection badge. There is no expiry and no
/// timer: a badge stays until an admin takes it off, so every change is one of these
/// two and every one of them is recorded with a reason — rule 112.
/// </summary>
public enum BadgeAction
{
    Granted,
    Removed
}
