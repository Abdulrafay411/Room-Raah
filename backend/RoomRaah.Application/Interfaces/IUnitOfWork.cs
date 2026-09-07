namespace RoomRaah.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();
}
