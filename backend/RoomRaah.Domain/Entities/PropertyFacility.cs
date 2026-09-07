namespace RoomRaah.Domain.Entities;

public class PropertyFacility
{
    public int PropertyId { get; set; }
    public int FacilityId { get; set; }

    public Property Property { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
}
