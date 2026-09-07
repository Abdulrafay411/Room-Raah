using MediatR;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

public class DeletePropertyPhotoCommandHandler : IRequestHandler<DeletePropertyPhotoCommand>
{
    private readonly IPropertyRepository _properties;
    private readonly IPropertyPhotoRepository _photos;
    private readonly IPropertyRevisionRepository _revisions;
    private readonly IPhotoStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePropertyPhotoCommandHandler(
        IPropertyRepository properties,
        IPropertyPhotoRepository photos,
        IPropertyRevisionRepository revisions,
        IPhotoStorageService storage,
        IUnitOfWork unitOfWork)
    {
        _properties = properties;
        _photos = photos;
        _revisions = revisions;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeletePropertyPhotoCommand request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        var photo = property.Photos.FirstOrDefault(p => p.Id == request.PhotoId);
        if (photo is null)
        {
            throw new NotFoundException(PropertyMessages.PhotoNotFound(request.PhotoId));
        }

        if (photo.Status == PhotoStatus.PendingRemove)
        {
            throw new BusinessRuleException(ReviewMessages.PhotoAlreadyPendingRemoval);
        }

        var published = property.Status == PropertyStatus.Published;

        // Rule 66. A photo that has never been public has nothing for an admin to
        // weigh, so withdrawing it is a real delete even on a published listing.
        var reallyDelete = !published || photo.Status == PhotoStatus.PendingAdd;

        if (published)
        {
            var revision = await PendingGallery.OpenOrJoinAsync(property, _revisions);
            PendingGallery.Forget(revision, photo.Id);

            if (!reallyDelete)
            {
                photo.Status = PhotoStatus.PendingRemove;
            }
        }

        if (reallyDelete)
        {
            // Rule 57. Losing the primary photo must not leave a listing with none,
            // so the lowest remaining position takes over in the same save. On a
            // published listing a pending photo is never primary, so this only ever
            // fires on a listing the public cannot see.
            if (photo.IsPrimary)
            {
                var successor = property.Photos
                    .Where(p => p.Id != photo.Id && p.Status != PhotoStatus.PendingRemove)
                    .OrderBy(p => p.SortOrder)
                    .FirstOrDefault();

                if (successor is not null)
                {
                    successor.IsPrimary = true;
                }
            }

            await _photos.DeleteAsync(photo);
            property.Photos.Remove(photo);
        }

        property.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        // Blobs go after the row, never before. A file removed ahead of a save that
        // then fails would leave a row pointing at nothing, which is the one failure
        // a visitor would actually see. A photo only marked for removal keeps its
        // files: the public is still being shown it.
        if (reallyDelete)
        {
            await _storage.DeleteAsync(photo.BlobUrl, photo.ThumbnailUrl);
        }
    }
}
