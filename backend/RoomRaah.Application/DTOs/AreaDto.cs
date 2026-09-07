namespace RoomRaah.Application.DTOs;

/// <summary>A neighbourhood within a city.</summary>
public class AreaDto
{
    /// <summary>Identifier, used as the area filter value and as a property's AreaId.</summary>
    public int Id { get; set; }

    /// <summary>Display name, for example "Gulberg".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The city this area belongs to.</summary>
    public int CityId { get; set; }
}
