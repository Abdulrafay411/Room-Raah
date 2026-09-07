using AutoMapper;
using MediatR;
using Microsoft.Extensions.Options;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

public class UploadPropertyPhotoCommandHandler
    : IRequestHandler<UploadPropertyPhotoCommand, PropertyPhotoDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IPropertyPhotoRepository _photos;
    private readonly IPropertyRevisionRepository _revisions;
    private readonly IPhotoStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PhotoSettings _photoSettings;
    private readonly IMapper _mapper;

    public UploadPropertyPhotoCommandHandler(
        IPropertyRepository properties,
        IPropertyPhotoRepository photos,
        IPropertyRevisionRepository revisions,
        IPhotoStorageService storage,
        IUnitOfWork unitOfWork,
        IOptions<PhotoSettings> photoSettings,
        IMapper mapper)
    {
        _properties = properties;
        _photos = photos;
        _revisions = revisions;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _photoSettings = photoSettings.Value;
        _mapper = mapper;
    }

    public async Task<PropertyPhotoDto> Handle(
        UploadPropertyPhotoCommand request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        var published = property.Status == PropertyStatus.Published;

        // Rule 55, checked before anything is uploaded, and counted over the gallery
        // the listing will end up with rather than the rows it holds right now: a
        // photo already on its way out does not take a place from a new one.
        var gallery = ListingGallery.Proposed(property);

        if (gallery.Count >= _photoSettings.MaxPhotosPerListing)
        {
            throw new BusinessRuleException(
                PropertyMessages.TooManyPhotos(_photoSettings.MaxPhotosPerListing));
        }

        // Rule 56. The thumbnail has to be decoded from these bytes anyway, so the
        // decode is the content check — a client controls its Content-Type header
        // exactly as easily as it controls a file extension.
        var stored = await _storage.UploadAsync(request.PropertyId, request.Content, request.FileName);
        if (stored is null)
        {
            throw new BusinessRuleException(PropertyMessages.PhotoNotReadable);
        }

        var photo = new PropertyPhoto
        {
            PropertyId = property.Id,
            BlobUrl = stored.BlobUrl,
            ThumbnailUrl = stored.ThumbnailUrl,
            // Rules 59 and 66. A listing nobody can see has no pending relationship to
            // a published version of itself, so its photos are simply live; on a
            // published one the photo is a proposal until an admin agrees to it.
            Status = published ? PhotoStatus.PendingAdd : PhotoStatus.Live,
            // Rule 57: the first upload is primary until the owner says otherwise. A
            // proposed photo never is — the published gallery keeps the primary it
            // was approved with until the revision carrying this one is applied.
            IsPrimary = !published && gallery.Count == 0,
            SortOrder = gallery.Count == 0 ? 1 : gallery.Max(p => p.SortOrder) + 1,
            UploadedAt = DateTime.UtcNow
        };

        await _photos.CreateAsync(photo);

        // Rule 67. The revision is what carries this photo into a queue; without one
        // nothing would ever look at it. It is not given a proposed order: a null
        // order means the gallery is otherwise unchanged, and an unnamed photo takes
        // a place at the end when the order is applied, which is where a new one goes.
        if (published)
        {
            await PendingGallery.OpenOrJoinAsync(property, _revisions);
        }

        property.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch
        {
            // The file is already in blob storage and the row that would have named
            // it is not going to exist. Take the file back out rather than leave a
            // blob nothing can reach or account for.
            await _storage.DeleteAsync(stored.BlobUrl, stored.ThumbnailUrl);
            throw;
        }

        return _mapper.Map<PropertyPhotoDto>(photo);
    }
}
