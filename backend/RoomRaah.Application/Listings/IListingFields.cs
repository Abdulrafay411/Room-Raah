namespace RoomRaah.Application.Listings;

/// <summary>
/// The fields an owner sends when creating a listing and when editing one. They are
/// the same fields with the same rules, so they are validated by the same code:
/// a second copy of sixteen validation rules is a second copy that drifts.
/// </summary>
public interface IListingFields
{
    string Title { get; }
    string Description { get; }
    string AddressLine { get; }
    double? Latitude { get; }
    double? Longitude { get; }
    string RoomType { get; }
    string GenderPolicy { get; }
    int? TotalBeds { get; }
    int? AvailableBeds { get; }
    decimal? MonthlyRent { get; }
    decimal? SecurityDeposit { get; }
    decimal? UtilitiesCharge { get; }
    decimal? MessCharge { get; }
    string? HouseRules { get; }
    int AreaId { get; }
    List<int> FacilityIds { get; }
}
