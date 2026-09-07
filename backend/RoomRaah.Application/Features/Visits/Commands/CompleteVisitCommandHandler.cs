using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Visits.Commands;

public class CompleteVisitCommandHandler : IRequestHandler<CompleteVisitCommand, VisitRequestDto>
{
    private readonly IVisitRequestRepository _visits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CompleteVisitCommandHandler> _logger;

    public CompleteVisitCommandHandler(
        IVisitRequestRepository visits,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CompleteVisitCommandHandler> logger)
    {
        _visits = visits;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<VisitRequestDto> Handle(
        CompleteVisitCommand request, CancellationToken cancellationToken)
    {
        var visit = await _visits.GetByIdAsync(request.VisitId)
            ?? throw new NotFoundException(SeekerMessages.VisitNotFound(request.VisitId));

        // Rule 90: the two people who were there, and nobody else.
        if (visit.SeekerId != request.UserId && visit.Property.OwnerId != request.UserId)
        {
            throw new UnauthorizedAccessException(SeekerMessages.VisitNotYours);
        }

        VisitLifecycle.RequireCompletable(visit, DateTime.UtcNow);

        visit.Status = VisitStatus.Completed;

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} completed visit request {VisitId}", request.UserId, visit.Id);

        return _mapper.Map<VisitRequestDto>(visit);
    }
}
