using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IVerificationCheckRepository
{
    Task CreateAsync(VerificationCheck check);
}
