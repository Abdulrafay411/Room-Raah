using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// Rule 57 says exactly one photo is primary, and rule 58 says the order is a
/// property of the whole set. Both are easy to satisfy for the photo being changed
/// and to break for the ones that were not, so every operation here rewrites the
/// position and the primary flag of every photo rather than the one that moved.
/// </summary>
public static class ListingGallery
{
    /// <summary>
    /// The photos the listing will hold once whatever is pending has been decided:
    /// what is live now, plus what has been proposed, minus what is on its way out.
    /// This is the set a reorder has to name, and the set the photo cap counts.
    /// </summary>
    public static List<PropertyPhoto> Proposed(Property property) =>
        property.Photos
            .Where(p => p.Status != PhotoStatus.PendingRemove)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToList();

    /// <summary>
    /// The photos whose status is Live, and only those. This is the settled gallery
    /// an approval or a rejection lays out once the losing photos are gone. It is not
    /// what the public sees: see <see cref="Public"/>.
    /// </summary>
    public static List<PropertyPhoto> Live(Property property) =>
        property.Photos
            .Where(p => p.Status == PhotoStatus.Live)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToList();

    /// <summary>
    /// Rule 79: what a stranger sees today. A photo waiting to be added has not been
    /// reviewed and must not appear; a photo waiting to be removed is still on the
    /// listing until an admin agrees to take it off. The card, the detail page and
    /// the photos endpoint all read this one set.
    /// </summary>
    public static List<PropertyPhoto> Public(Property property) =>
        property.Photos
            .Where(p => p.Status != PhotoStatus.PendingAdd)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToList();

    /// <summary>
    /// Lays the given ids out as positions 1..n and makes the first one primary.
    /// Any photo not named keeps a position after them, which only happens on the
    /// live gallery after a rejection has removed photos from underneath an order.
    /// </summary>
    public static void ApplyOrder(Property property, IReadOnlyList<int> order)
    {
        var position = 1;

        foreach (var id in order)
        {
            var photo = property.Photos.FirstOrDefault(p => p.Id == id);
            if (photo is null)
            {
                continue;
            }

            photo.SortOrder = position;
            photo.IsPrimary = position == 1;
            position++;
        }

        foreach (var photo in property.Photos.Where(p => !order.Contains(p.Id)))
        {
            photo.SortOrder = position++;
            photo.IsPrimary = false;
        }
    }

    /// <summary>
    /// Closes the gaps a delete leaves and guarantees rule 57 over whatever is left.
    /// Called wherever photos disappear without an order being supplied.
    /// </summary>
    public static void Renumber(Property property)
    {
        ApplyOrder(property, Live(property).Select(p => p.Id).ToList());
    }
}
