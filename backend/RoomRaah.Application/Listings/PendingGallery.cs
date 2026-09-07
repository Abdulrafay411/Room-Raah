using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// Rule 67. A photo change on a published listing is a proposal, and a proposal that
/// no queue carries is a change nobody will ever approve — so every photo operation
/// on a published listing opens a content revision if none is pending, and joins the
/// one that is.
/// </summary>
public static class PendingGallery
{
    public static async Task<PropertyRevision> OpenOrJoinAsync(
        Property property, IPropertyRevisionRepository revisions)
    {
        var revision = await revisions.GetPendingAsync(property.Id, RevisionKind.Content);

        if (revision is null)
        {
            revision = ListingRevisions.Snapshot(property, RevisionKind.Content);
            await revisions.CreateAsync(revision);
        }

        return revision;
    }

    /// <summary>
    /// A null order means the gallery is unchanged, which is the common case and is
    /// worth keeping distinguishable from an order that happens to match. The first
    /// operation that actually moves something fills it in from what is there now.
    /// </summary>
    public static List<int> EnsureOrder(PropertyRevision revision, Property property)
    {
        var order = revision.PhotoOrder is null
            ? ListingGallery.Proposed(property).Select(p => p.Id).ToList()
            : IdList.Parse(revision.PhotoOrder);

        revision.PhotoOrder = IdList.Format(order);

        return order;
    }

    public static void SetOrder(PropertyRevision revision, IEnumerable<int> order)
    {
        revision.PhotoOrder = IdList.Format(order);
    }

    /// <summary>
    /// Takes an id out of a proposed order if it is named there. A photo can leave
    /// the listing after the order was written, and an order naming a photo that no
    /// longer exists is an order rule 68 would reject.
    /// </summary>
    public static void Forget(PropertyRevision revision, int photoId)
    {
        if (revision.PhotoOrder is null)
        {
            return;
        }

        revision.PhotoOrder = IdList.Format(IdList.Parse(revision.PhotoOrder).Where(id => id != photoId));
    }
}
