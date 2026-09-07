namespace RoomRaah.Application.Listings;

/// <summary>
/// Every message a listing or photo failure can return, defined once so the same
/// rule reads the same way wherever it is enforced.
/// </summary>
public static class PropertyMessages
{
    public const string OwnerProfileRequired = "Only an owner account may create a listing.";
    public const string NotYourListing = "This listing belongs to another account.";
    public const string AreaNotFound = "The area given does not exist.";
    public const string FacilityNotFound = "One or more of the facilities given do not exist.";
    public const string NotSubmittable = "Only a draft or a listing with changes requested can be submitted.";
    public const string PhotosLockedWhilePublished =
        "A published listing's photos are changed through a revision, not directly.";
    public const string PhotoNotReadable =
        "This file could not be read as a JPEG, PNG or WebP image.";
    public const string ReorderMustNameEveryPhoto =
        "The new order must name every photo on this listing exactly once.";

    public static string PropertyNotFound(int id) => $"Property {id} was not found.";

    public static string PhotoNotFound(int id) => $"Photo {id} was not found on this listing.";

    public static string TooFewPhotos(int minimum) =>
        $"A listing needs at least {minimum} photos before it can be submitted.";

    public static string TooManyPhotos(int maximum) =>
        $"A listing may hold at most {maximum} photos.";
}
