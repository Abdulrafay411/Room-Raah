using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

public class SubmitPropertyCommandHandler : IRequestHandler<SubmitPropertyCommand, OwnerPropertyDto>
{
    private static readonly PropertyStatus[] Submittable =
    [
        PropertyStatus.Draft,
        PropertyStatus.ChangesRequested
    ];

    private readonly IPropertyRepository _properties;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PhotoSettings _photoSettings;
    private readonly IMapper _mapper;
    private readonly ILogger<SubmitPropertyCommandHandler> _logger;

    public SubmitPropertyCommandHandler(
        IPropertyRepository properties,
        IUnitOfWork unitOfWork,
        IOptions<PhotoSettings> photoSettings,
        IMapper mapper,
        ILogger<SubmitPropertyCommandHandler> logger)
    {
        _properties = properties;
        _unitOfWork = unitOfWork;
        _photoSettings = photoSettings.Value;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<OwnerPropertyDto> Handle(SubmitPropertyCommand request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        // Rule 52. ChangesRequested is an admin asking for a fix, so resubmitting is
        // the point of that status; every other status is either already in the
        // queue or a decision somebody made.
        if (!Submittable.Contains(property.Status))
        {
            throw new BusinessRuleException(PropertyMessages.NotSubmittable);
        }

        // Rule 15, counted over Live photos. On a listing that has never been
        // published every photo is Live, which is what makes the count meaningful.
        var live = property.Photos.Count(p => p.Status == PhotoStatus.Live);

        if (live < _photoSettings.MinPhotosPerListing)
        {
            throw new BusinessRuleException(
                PropertyMessages.TooFewPhotos(_photoSettings.MinPhotosPerListing));
        }

        if (live > _photoSettings.MaxPhotosPerListing)
        {
            throw new BusinessRuleException(
                PropertyMessages.TooManyPhotos(_photoSettings.MaxPhotosPerListing));
        }

        property.Status = PropertyStatus.PendingReview;
        property.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Property {PropertyId} submitted for review by owner {OwnerId}",
            property.Id, request.OwnerId);

        return _mapper.Map<OwnerPropertyDto>(property);
    }
}
