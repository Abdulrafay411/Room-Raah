using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class VerificationCheckRepository : IVerificationCheckRepository
{
    private readonly RoomRaahDbContext _context;

    public VerificationCheckRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(VerificationCheck check)
    {
        _context.VerificationChecks.Add(check);
        return Task.CompletedTask;
    }
}
