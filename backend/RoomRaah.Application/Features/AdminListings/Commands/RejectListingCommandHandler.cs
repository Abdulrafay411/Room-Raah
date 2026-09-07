using AutoMapper;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class RejectListingCommandHandler : ListingDecisionHandler<RejectListingCommand>
{
    public RejectListingCommandHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RejectListingCommandHandler> logger)
        : base(properties, cache, email, unitOfWork, mapper, logger)
    {
    }

    protected override PropertyStatus Outcome => PropertyStatus.Rejected;

    protected override bool NoteRequired => true;

    protected override string DecisionText => "Rejected";
}
