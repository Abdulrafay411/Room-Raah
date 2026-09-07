namespace RoomRaah.Application.DTOs;

/// <summary>A city the product has listings in.</summary>
public class CityDto
{
    /// <summary>Identifier, used as the city filter value.</summary>
    public int Id { get; set; }

    /// <summary>Display name, for example "Lahore".</summary>
    public string Name { get; set; } = string.Empty;
}
