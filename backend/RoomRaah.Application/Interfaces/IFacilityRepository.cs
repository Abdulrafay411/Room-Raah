using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IFacilityRepository
{
    Task<List<Facility>> GetAllAsync();
    Task<List<Facility>> GetByIdsAsync(IReadOnlyCollection<int> ids);
}
