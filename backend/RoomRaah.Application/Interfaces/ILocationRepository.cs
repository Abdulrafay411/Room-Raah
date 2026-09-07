using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface ILocationRepository
{
    Task<List<City>> GetCitiesAsync();
    Task<List<Area>> GetAreasAsync(int cityId);
    Task<bool> CityExistsAsync(int cityId);
    Task<bool> AreaExistsAsync(int areaId);

    /// <summary>The landmark itself, because the distance filter needs its coordinates.</summary>
    Task<Landmark?> GetLandmarkAsync(int landmarkId);
    Task<List<City>> SearchCitiesAsync(string term, int limit);
    Task<List<Area>> SearchAreasAsync(string term, int limit);
    Task<List<Landmark>> SearchLandmarksAsync(string term, int limit);
}
