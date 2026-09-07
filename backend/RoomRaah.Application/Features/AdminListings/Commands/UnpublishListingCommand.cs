using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Commands;

/// <summary>
/// Rule 122. §5 had ten admin listing endpoints and none of them set
/// <c>Unpublished</c>, so the status existed in the enumeration and was unreachable in
/// the product — a listing could never leave the public site through the API at all.
/// Phase 6 recorded the gap and deliberately did not close it, because upholding a
/// report is not the same decision as taking a listing down.
/// </summary>
public class UnpublishListingCommand : IRequest<AdminPropertyDto>, IListingDecision
{
    public int PropertyId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int AdminId { get; set; }

    /// <summary>Required. Rule 122, for the same reason rule 26 requires one.</summary>
    public string? AdminNote { get; set; }
}
