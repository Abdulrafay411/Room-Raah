using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Listings;

/// <summary>
/// The two checks every owner-facing listing handler has to make, in one place so
/// that a handler cannot enforce one and forget the other. Rule 18: an owner may
/// read and modify only their own listings.
/// </summary>
public static class OwnedListing
{
    public static Property Require(Property? property, int propertyId, int ownerId)
    {
        if (property is null)
        {
            throw new NotFoundException(PropertyMessages.PropertyNotFound(propertyId));
        }

        if (property.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException(PropertyMessages.NotYourListing);
        }

        return property;
    }

    /// <summary>
    /// Rule 54. A published listing changes through a revision, which is the admin
    /// phase's work; until then a direct write to one is refused rather than allowed
    /// through and quietly published.
    /// </summary>
    public static void RequireNotPublished(Property property)
    {
        if (property.Status == Domain.Enums.PropertyStatus.Published)
        {
            throw new BusinessRuleException(PropertyMessages.PhotosLockedWhilePublished);
        }
    }
}
