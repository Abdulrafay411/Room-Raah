using RoomRaah.Application.Interfaces;

namespace RoomRaah.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly RoomRaahDbContext _context;

    public UnitOfWork(RoomRaahDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
