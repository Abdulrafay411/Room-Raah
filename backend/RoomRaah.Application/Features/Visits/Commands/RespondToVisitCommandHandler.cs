using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Visits.Commands;

public class RespondToVisitCommandHandler : IRequestHandler<RespondToVisitCommand, VisitRequestDto>
{
    private readonly IVisitRequestRepository _visits;
    private readonly IEmailSender _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<RespondToVisitCommandHandler> _logger;

    public RespondToVisitCommandHandler(
        IVisitRequestRepository visits,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RespondToVisitCommandHandler> logger)
    {
        _visits = visits;
        _email = email;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<VisitRequestDto> Handle(
        RespondToVisitCommand request, CancellationToken cancellationToken)
    {
        var visit = await _visits.GetByIdAsync(request.VisitId)
            ?? throw new NotFoundException(SeekerMessages.VisitNotFound(request.VisitId));

        // Rule 37.
        if (visit.Property.OwnerId != request.OwnerId)
        {
            throw new UnauthorizedAccessException(SeekerMessages.VisitNotYours);
        }

        VisitLifecycle.RequireAnswerable(visit);

        var answer = Enum.Parse<VisitStatus>(request.Status, ignoreCase: true);
        var note = VisitLifecycle.RequireNoteOnDecline(answer, request.OwnerResponseNote);

        visit.Status = answer;
        visit.OwnerResponseNote = note;
        visit.RespondedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Owner {OwnerId} set visit request {VisitId} to {Status}",
            request.OwnerId, visit.Id, answer);

        // Rule 92.
        await _email.SendVisitResponseAsync(
            visit.Seeker, visit.Property.Title, answer.ToString(), note);

        return _mapper.Map<VisitRequestDto>(visit);
    }
}
