namespace RoomRaah.Domain.Enums;

/// <summary>
/// How close the author actually got to the room. A reader weighs "I looked at it"
/// and "I have lived here for a year" very differently, and rule 38 lets both write.
/// </summary>
public enum StayStatus
{
    Visited,
    CurrentlyStaying,
    PreviouslyStayed
}
