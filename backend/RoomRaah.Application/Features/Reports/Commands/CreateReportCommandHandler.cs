using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Reports.Commands;

public class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, MyReportDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateReportCommandHandler> _logger;

    public CreateReportCommandHandler(
        IPropertyRepository properties,
        IReportRepository reports,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateReportCommandHandler> logger)
    {
        _properties = properties;
        _reports = reports;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<MyReportDto> Handle(CreateReportCommand request, CancellationToken cancellationToken)
    {
        // Rule 86.
        var property = await _properties.GetPublishedWithOwnerAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        // Rule 42 as amended. A second report from the same person about the same
        // listing while the first is still being looked at is the same complaint
        // twice, and it pushes a real one down the queue.
        if (await _reports.HasUnresolvedAsync(request.ReportedByUserId, property.Id))
        {
            throw new BusinessRuleException(SeekerMessages.ReportAlreadyOpen);
        }

        var report = new Report
        {
            PropertyId = property.Id,
            ReportedByUserId = request.ReportedByUserId,
            Reason = Enum.Parse<ReportReason>(request.Reason, ignoreCase: true),
            Details = request.Details.Trim(),
            Status = ReportStatus.Open
        };

        await _reports.CreateAsync(report);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} reported property {PropertyId} as {Reason}",
            request.ReportedByUserId, property.Id, report.Reason);

        return _mapper.Map<MyReportDto>(report);
    }
}
