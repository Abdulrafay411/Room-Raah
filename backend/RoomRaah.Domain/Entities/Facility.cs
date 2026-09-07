namespace RoomRaah.Domain.Entities;

public class Facility
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
