namespace RoomRaah.Application.DTOs;

/// <summary>
/// One row of the location autocomplete. The list mixes cities, areas and
/// landmarks, so every row states which of the three it is.
/// </summary>
public class LocationSuggestionDto
{
    public const string CityType = "City";
    public const string AreaType = "Area";
    public const string LandmarkType = "Landmark";

    /// <summary>One of City, Area or Landmark. Tells the client which filter to apply.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Identifier within the type named above.</summary>
    public int Id { get; set; }

    /// <summary>Display name, for example "NUST" or "Gulberg".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The city this sits in, so "Gulberg" can be told apart from the Gulberg in
    /// another city. Null on a city row, which is already the city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>University, Workplace or Other. Null unless this row is a landmark.</summary>
    public string? Kind { get; set; }

    /// <summary>Null unless this row is a landmark — only landmarks anchor the distance filter.</summary>
    public double? Latitude { get; set; }

    /// <summary>Null unless this row is a landmark.</summary>
    public double? Longitude { get; set; }
}
