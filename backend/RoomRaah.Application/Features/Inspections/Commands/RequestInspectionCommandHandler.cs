using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Inspections.Commands;

public class RequestInspectionCommandHandler
    : IRequestHandler<RequestInspectionCommand, OwnerInspectionStateDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<RequestInspectionCommandHandler> _logger;

    public RequestInspectionCommandHandler(
        IPropertyRepository properties,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RequestInspectionCommandHandler> logger)
    {
        _properties = properties;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<OwnerInspectionStateDto> Handle(
        RequestInspectionCommand request, CancellationToken cancellationToken)
    {
        // Rule 108, first half: their own listing, and nobody else's.
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId),
            request.PropertyId,
            request.OwnerId);

        // A draft has nothing to inspect against — the inspection checks the property
        // against the listing, and until it is published there is no listing to check.
        if (property.Status != PropertyStatus.Published)
        {
            throw new BusinessRuleException(InspectionMessages.OnlyPublishedCanBeInspected);
        }

        // Rule 108, second half. The owner asks once; after that the team goes on its
        // own schedule, unannounced, which is what §Business Model says the fee buys.
        if (property.InspectionRequestedAt is not null)
        {
            throw new BusinessRuleException(InspectionMessages.InspectionAlreadyRequested);
        }

        property.InspectionRequestedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Owner {OwnerId} requested an inspection of property {PropertyId}",
            request.OwnerId, property.Id);

        return _mapper.Map<OwnerInspectionStateDto>(property);
    }
}
