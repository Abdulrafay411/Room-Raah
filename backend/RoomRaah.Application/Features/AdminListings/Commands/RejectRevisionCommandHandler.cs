using AutoMapper;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class RejectRevisionCommandHandler : RevisionDecisionHandler<RejectRevisionCommand>
{
    public RejectRevisionCommandHandler(
        IPropertyRevisionRepository revisions,
        IPropertyPhotoRepository photos,
        IPhotoStorageService storage,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RejectRevisionCommandHandler> logger)
        : base(revisions, photos, storage, cache, email, unitOfWork, mapper, logger)
    {
    }

    protected override RevisionStatus Outcome => RevisionStatus.Rejected;

    protected override bool NoteRequired => true;

    protected override string DecisionText => "Rejected";

    /// <summary>Rule 27. Nothing on the live listing moves.</summary>
    protected override List<PropertyPhoto> Settle(Property property, PropertyRevision revision, DateTime now)
        => RevisionApplication.Reject(property);
}
