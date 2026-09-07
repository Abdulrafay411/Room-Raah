using AutoMapper;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class ApproveListingCommandHandler : ListingDecisionHandler<ApproveListingCommand>
{
    public ApproveListingCommandHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<ApproveListingCommandHandler> logger)
        : base(properties, cache, email, unitOfWork, mapper, logger)
    {
    }

    protected override PropertyStatus Outcome => PropertyStatus.Published;

    /// <summary>
    /// Rule 124. A listing an admin took off the site comes back through here rather
    /// than through a second endpoint, because putting it back is the same decision it
    /// was the first time — and rule 23's four checks are still in front of it.
    /// </summary>
    protected override PropertyStatus[] AllowedFrom =>
        [PropertyStatus.PendingReview, PropertyStatus.Unpublished];

    protected override bool NoteRequired => false;

    protected override string DecisionText => "Approved and published";

    /// <summary>Rule 23.</summary>
    protected override void Guard(Property property)
    {
        if (!ListingReview.ChecksPass(property))
        {
            throw new BusinessRuleException(ReviewMessages.ChecksIncomplete);
        }
    }

    /// <summary>
    /// Rule 24. Publishing is the moment the bed count starts being a claim to the
    /// public, so it is also the moment the clock on that claim starts.
    /// </summary>
    protected override void Apply(Property property, DateTime now)
    {
        property.AvailabilityConfirmedAt = now;
    }
}
