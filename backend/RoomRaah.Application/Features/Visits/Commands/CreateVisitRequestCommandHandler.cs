using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Visits.Commands;

public class CreateVisitRequestCommandHandler
    : IRequestHandler<CreateVisitRequestCommand, VisitRequestDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IVisitRequestRepository _visits;
    private readonly IEmailSender _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateVisitRequestCommandHandler> _logger;

    public CreateVisitRequestCommandHandler(
        IPropertyRepository properties,
        IVisitRequestRepository visits,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateVisitRequestCommandHandler> logger)
    {
        _properties = properties;
        _visits = visits;
        _email = email;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<VisitRequestDto> Handle(
        CreateVisitRequestCommand request, CancellationToken cancellationToken)
    {
        // Rule 86.
        var property = await _properties.GetPublishedWithOwnerAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        // Rule 36. Open means Requested or Accepted: a declined or cancelled request
        // is finished business, and a seeker whose plans changed may ask again.
        if (await _visits.HasOpenAsync(request.SeekerId, property.Id))
        {
            throw new BusinessRuleException(SeekerMessages.VisitAlreadyOpen);
        }

        var visit = new VisitRequest
        {
            PropertyId = property.Id,
            SeekerId = request.SeekerId,
            VisitType = Enum.Parse<VisitType>(request.VisitType, ignoreCase: true),
            PreferredAt = request.PreferredAt,
            Status = VisitStatus.Requested
        };

        await _visits.CreateAsync(visit);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Seeker {SeekerId} requested a {VisitType} visit to property {PropertyId}",
            request.SeekerId, visit.VisitType, property.Id);

        // Rule 92. There is no phone number anywhere in this product, so this email is
        // the only thing that tells an owner there is something to log in for.
        await _email.SendVisitRequestedAsync(
            property.Owner, property.Title, visit.VisitType, visit.PreferredAt);

        // Reloaded so the response carries the listing and the seeker the same way the
        // two list endpoints do, rather than a half-populated graph the writer happens
        // to be holding.
        var saved = await _visits.GetByIdAsync(visit.Id);

        return _mapper.Map<VisitRequestDto>(saved!);
    }
}
