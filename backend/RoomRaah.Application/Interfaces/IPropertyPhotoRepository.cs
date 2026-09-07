using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IPropertyPhotoRepository
{
    Task CreateAsync(PropertyPhoto photo);
    Task DeleteAsync(PropertyPhoto photo);
}
