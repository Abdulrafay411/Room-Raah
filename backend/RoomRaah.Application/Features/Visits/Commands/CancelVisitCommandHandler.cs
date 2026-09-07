using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Visits.Commands;

public class CancelVisitCommandHandler : IRequestHandler<CancelVisitCommand, VisitRequestDto>
{
    private readonly IVisitRequestRepository _visits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CancelVisitCommandHandler> _logger;

    public CancelVisitCommandHandler(
        IVisitRequestRepository visits,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CancelVisitCommandHandler> logger)
    {
        _visits = visits;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<VisitRequestDto> Handle(
        CancelVisitCommand request, CancellationToken cancellationToken)
    {
        var visit = await _visits.GetByIdAsync(request.VisitId)
            ?? throw new NotFoundException(SeekerMessages.VisitNotFound(request.VisitId));

        // Rule 91: withdrawing a request is the seeker's alone. An owner who does not
        // want a visit declines it, which says so and carries a reason.
        if (visit.SeekerId != request.SeekerId)
        {
            throw new UnauthorizedAccessException(SeekerMessages.VisitNotYours);
        }

        VisitLifecycle.RequireCancellable(visit);

        visit.Status = VisitStatus.Cancelled;

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Seeker {SeekerId} cancelled visit request {VisitId}", request.SeekerId, visit.Id);

        return _mapper.Map<VisitRequestDto>(visit);
    }
}
