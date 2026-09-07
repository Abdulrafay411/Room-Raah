using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Reports.Commands;

public class ResolveReportCommandHandler : IRequestHandler<ResolveReportCommand, ReportDto>
{
    private readonly IReportRepository _reports;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<ResolveReportCommandHandler> _logger;

    public ResolveReportCommandHandler(
        IReportRepository reports,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<ResolveReportCommandHandler> logger)
    {
        _reports = reports;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ReportDto> Handle(ResolveReportCommand request, CancellationToken cancellationToken)
    {
        var report = await _reports.GetByIdAsync(request.ReportId)
            ?? throw new NotFoundException(SeekerMessages.ReportNotFound(request.ReportId));

        var target = Enum.Parse<ReportStatus>(request.Status, ignoreCase: true);

        ModerationRules.RequireResolvable(report, target);

        var note = ModerationRules.RequireNoteOnVerdict(target, request.AdminNote);

        report.Status = target;
        report.AdminNote = note;

        // Rule 98: the verdict is recorded and the listing is left alone. A listing
        // comes down because an admin decided to take it down, on the admin listing
        // endpoints, and not as a side effect of answering a complaint.
        if (ModerationRules.IsVerdict(target))
        {
            report.ResolvedByAdminId = request.AdminId;
            report.ResolvedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} set report {ReportId} to {Status}", request.AdminId, report.Id, target);

        return _mapper.Map<ReportDto>(report);
    }
}
