namespace RoomRaah.Application.DTOs;

/// <summary>A facility a listing can offer, from the fixed reference list.</summary>
public class FacilityDto
{
    /// <summary>Identifier, used as the facility filter value.</summary>
    public int Id { get; set; }

    /// <summary>Display name, for example "Wi-Fi".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable key the client maps to an icon, for example "wifi".</summary>
    public string IconKey { get; set; } = string.Empty;
}
