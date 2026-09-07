using AutoMapper;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class RequestListingChangesCommandHandler : ListingDecisionHandler<RequestListingChangesCommand>
{
    public RequestListingChangesCommandHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RequestListingChangesCommandHandler> logger)
        : base(properties, cache, email, unitOfWork, mapper, logger)
    {
    }

    protected override PropertyStatus Outcome => PropertyStatus.ChangesRequested;

    protected override bool NoteRequired => true;

    protected override string DecisionText => "Changes requested";
}
