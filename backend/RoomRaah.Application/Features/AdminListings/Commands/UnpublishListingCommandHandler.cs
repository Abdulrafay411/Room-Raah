using AutoMapper;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Commands;

/// <summary>
/// Rule 122. It is a listing decision like the other three, so it inherits all of
/// them: the note, the cache eviction rule 123 asks for, the email to the owner and
/// the log line. Only the statuses it starts from and the status it leaves behind are
/// different.
/// </summary>
public class UnpublishListingCommandHandler : ListingDecisionHandler<UnpublishListingCommand>
{
    public UnpublishListingCommandHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UnpublishListingCommandHandler> logger)
        : base(properties, cache, email, unitOfWork, mapper, logger)
    {
    }

    protected override PropertyStatus Outcome => PropertyStatus.Unpublished;

    /// <summary>Rule 122: only from `Published`.</summary>
    protected override PropertyStatus[] AllowedFrom => [PropertyStatus.Published];

    protected override string NotAllowedMessage => InspectionMessages.NotPublished;

    /// <summary>
    /// Required. An owner whose listing disappears without a reason has nothing to act
    /// on and no way to get it back, which is the same dead end rule 26 exists to close.
    /// </summary>
    protected override bool NoteRequired => true;

    protected override string DecisionText => "Taken off the site";
}
