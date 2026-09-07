namespace RoomRaah.Domain.Enums;

/// <summary>
/// What a RoomRaah team member found when they visited. Only <see cref="Passed"/> can
/// lead to a badge — rule 110 — and a <see cref="Failed"/> visit is recorded rather
/// than discarded, because the record of a visit that went badly is worth more than
/// the record of one that went well.
/// </summary>
public enum InspectionResult
{
    Passed,
    Failed
}
