using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

public class ReorderPropertyPhotosCommandHandler
    : IRequestHandler<ReorderPropertyPhotosCommand, OwnerListingUpdateDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IPropertyRevisionRepository _revisions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ReorderPropertyPhotosCommandHandler(
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
        ReorderPropertyPhotosCommand request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        // Rules 58 and 68. A partial list has no single sensible reading, so the whole
        // order is required: same count, no duplicates, and nothing named that is not
        // going to be there. On a published listing "there" means the gallery the
        // revision will produce, which includes a proposed photo and excludes one on
        // its way out.
        var gallery = ListingGallery.Proposed(property);
        var requested = request.PhotoIds;
        var expected = gallery.Select(p => p.Id).ToHashSet();

        var namesEveryPhotoOnce =
            requested.Count == expected.Count &&
            requested.Distinct().Count() == requested.Count &&
            requested.All(expected.Contains);

        if (!namesEveryPhotoOnce)
        {
            throw new BusinessRuleException(PropertyMessages.ReorderMustNameEveryPhoto);
        }

        PropertyRevision? revision = null;

        if (property.Status == PropertyStatus.Published)
        {
            revision = await PendingGallery.OpenOrJoinAsync(property, _revisions);
            PendingGallery.SetOrder(revision, requested);
        }
        else
        {
            for (var position = 0; position < requested.Count; position++)
            {
                property.Photos.First(p => p.Id == requested[position]).SortOrder = position + 1;
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
