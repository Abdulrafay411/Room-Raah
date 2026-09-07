using AutoMapper;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class ApproveRevisionCommandHandler : RevisionDecisionHandler<ApproveRevisionCommand>
{
    public ApproveRevisionCommandHandler(
        IPropertyRevisionRepository revisions,
        IPropertyPhotoRepository photos,
        IPhotoStorageService storage,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<ApproveRevisionCommandHandler> logger)
        : base(revisions, photos, storage, cache, email, unitOfWork, mapper, logger)
    {
    }

    protected override RevisionStatus Outcome => RevisionStatus.Approved;

    protected override bool NoteRequired => false;

    protected override string DecisionText => "Approved";

    protected override List<PropertyPhoto> Settle(Property property, PropertyRevision revision, DateTime now)
        => RevisionApplication.Approve(property, revision, now);
}
