using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

public class SetPrimaryPropertyPhotoCommandHandler
    : IRequestHandler<SetPrimaryPropertyPhotoCommand, OwnerListingUpdateDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IPropertyRevisionRepository _revisions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SetPrimaryPropertyPhotoCommandHandler(
        IPropertyRepository properties,
        IPropertyRevisionRepository revisions,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _properties = properties;
        _revisions = revisions;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<OwnerListingUpdateDto> Handle(
        SetPrimaryPropertyPhotoCommand request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        // A photo on its way out cannot be chosen to lead the gallery it is leaving.
        if (ListingGallery.Proposed(property).All(p => p.Id != request.PhotoId))
        {
            throw new NotFoundException(PropertyMessages.PhotoNotFound(request.PhotoId));
        }

        PropertyRevision? revision = null;

        if (property.Status == PropertyStatus.Published)
        {
            revision = await PendingGallery.OpenOrJoinAsync(property, _revisions);
            revision.PrimaryPhotoId = request.PhotoId;
        }
        else
        {
            // Rule 57: exactly one. Assigning every photo rather than only the two that
            // change is what makes that true even if the data arrived with none or two.
            foreach (var photo in property.Photos)
            {
                photo.IsPrimary = photo.Id == request.PhotoId;
            }
        }

        property.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        var saved = await _properties.GetByIdAsync(property.Id);

        return new OwnerListingUpdateDto
        {
            Listing = _mapper.Map<OwnerPropertyDto>(saved),
            PendingRevision = revision is null ? null : RevisionView.Build(revision, saved!, _mapper)
        };
    }
}
