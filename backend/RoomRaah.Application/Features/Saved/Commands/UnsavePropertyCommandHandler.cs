using MediatR;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Saved.Commands;

public class UnsavePropertyCommandHandler : IRequestHandler<UnsavePropertyCommand>
{
    private readonly ISavedPropertyRepository _saved;
    private readonly IUnitOfWork _unitOfWork;

    public UnsavePropertyCommandHandler(ISavedPropertyRepository saved, IUnitOfWork unitOfWork)
    {
        _saved = saved;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UnsavePropertyCommand request, CancellationToken cancellationToken)
    {
        // Rule 87: this deliberately does not ask whether the listing is still
        // published. A shortlist that could not be cleared of a listing an admin had
        // taken down would be a list with a row nobody could ever remove.
        var saved = await _saved.GetAsync(request.UserId, request.PropertyId)
            ?? throw new NotFoundException(SeekerMessages.NotSaved);

        _saved.Remove(saved);
        await _unitOfWork.SaveChangesAsync();
    }
}
