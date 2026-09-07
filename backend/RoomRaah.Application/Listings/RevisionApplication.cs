using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// What an approval and a rejection do to the listing underneath a revision. Both
/// return the photos that lost — a rejected proposal or an approved removal — because
/// each of those has a blob and a thumbnail behind it that the caller has to delete
/// after the row is gone, and the caller is the one holding the storage service.
/// </summary>
public static class RevisionApplication
{
    /// <summary>Rules 25, 69 and 73.</summary>
    public static List<PropertyPhoto> Approve(Property property, PropertyRevision revision, DateTime now)
    {
        if (revision.Kind == RevisionKind.Availability)
        {
            // Rule 73: the bed count and nothing else. A content revision open at the
            // same time still holds a copy of the count as it was when it was written,
            // and writing that back would undo this the moment it is approved.
            property.AvailableBeds = revision.AvailableBeds;
            property.AvailabilityConfirmedAt = now;

            return [];
        }

        var proposed = ProposedListing.From(revision);

        // Rule 73 from the other side. A content revision carries the bed count it
        // captured when it was written, and it does not own it: an availability
        // revision approved in the meantime would be silently undone by writing it
        // back. Held aside and put straight back, so the copy in the revision is
        // read by the diff and applied by nothing.
        var beds = property.AvailableBeds;

        ListingRevisions.ApplyToListing(property, proposed);

        property.AvailableBeds = beds;

        property.Facilities.Clear();
        foreach (var id in proposed.FacilityIds)
        {
            property.Facilities.Add(new PropertyFacility { PropertyId = property.Id, FacilityId = id });
        }

        var losers = property.Photos.Where(p => p.Status == PhotoStatus.PendingRemove).ToList();

        foreach (var photo in property.Photos.Where(p => p.Status == PhotoStatus.PendingAdd))
        {
            photo.Status = PhotoStatus.Live;
        }

        return losers;
    }

    /// <summary>Rule 69, and rule 27: the live listing is left exactly as it was.</summary>
    public static List<PropertyPhoto> Reject(Property property)
    {
        var losers = property.Photos.Where(p => p.Status == PhotoStatus.PendingAdd).ToList();

        foreach (var photo in property.Photos.Where(p => p.Status == PhotoStatus.PendingRemove))
        {
            photo.Status = PhotoStatus.Live;
        }

        return losers;
    }

    /// <summary>
    /// Lays out the gallery once the losing photos are gone. An approved revision
    /// uses the order and the primary it proposed, where it proposed any; both are
    /// null when the owner changed neither, and then the live gallery simply closes
    /// the gaps the removals left.
    /// </summary>
    public static void Settle(Property property, PropertyRevision? revision)
    {
        var order = revision?.PhotoOrder is null
            ? ListingGallery.Live(property).Select(p => p.Id).ToList()
            : IdList.Parse(revision.PhotoOrder).Where(id => property.Photos.Any(p => p.Id == id)).ToList();

        ListingGallery.ApplyOrder(property, order);

        if (revision?.PrimaryPhotoId is not { } primaryId)
        {
            return;
        }

        // Rule 57 stays true either way: if the chosen photo is no longer here, the
        // one at the front of the order keeps the flag ApplyOrder just gave it.
        if (property.Photos.Any(p => p.Id == primaryId && p.Status == PhotoStatus.Live))
        {
            foreach (var photo in property.Photos)
            {
                photo.IsPrimary = photo.Id == primaryId;
            }
        }
    }
}
