using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class PropertyPhotoRepository : IPropertyPhotoRepository
{
    private readonly RoomRaahDbContext _context;

    public PropertyPhotoRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(PropertyPhoto photo)
    {
        _context.PropertyPhotos.Add(photo);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The one row in this product that is really removed rather than deactivated.
    /// A photo has no history worth keeping and its blob is about to go with it.
    /// </summary>
    public Task DeleteAsync(PropertyPhoto photo)
    {
        _context.PropertyPhotos.Remove(photo);
        return Task.CompletedTask;
    }
}
