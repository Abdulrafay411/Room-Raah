namespace RoomRaah.Application.Listings;

/// <summary>
/// What the public side of the product says when it refuses. Nothing here names a
/// status, an owner or a reason a listing is not published: rule 28 makes every
/// unpublished listing a 404, and a message that explained why would undo that.
/// </summary>
public static class SearchMessages
{
    public static string ListingNotFound(int id) => $"No published listing with id {id}.";

    public static string UnknownCity(int id) => $"City {id} does not exist.";

    public static string UnknownArea(int id) => $"Area {id} does not exist.";

    public static string UnknownLandmark(int id) => $"Landmark {id} does not exist.";

    public static string UnknownFacilities(IEnumerable<int> ids) =>
        $"These facilities do not exist: {IdList.Format(ids)}.";

    public const string TooManyToCompare = "At most 3 listings may be compared at once.";
}
