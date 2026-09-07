using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Reviews.Commands;

public class ModerateReviewCommandHandler : IRequestHandler<ModerateReviewCommand, AdminReviewDto>
{
    private readonly IReviewRepository _reviews;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<ModerateReviewCommandHandler> _logger;

    public ModerateReviewCommandHandler(
        IReviewRepository reviews,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<ModerateReviewCommandHandler> logger)
    {
        _reviews = reviews;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<AdminReviewDto> Handle(
        ModerateReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId)
            ?? throw new NotFoundException(SeekerMessages.ReviewNotFound(request.ReviewId));

        var state = Enum.Parse<ModerationState>(request.State, ignoreCase: true);

        // Checked again here, not only in the validator: the validator can be bypassed
        // by any future caller that builds the command itself, and this is the line
        // between a review being on the public site and not.
        ModerationRules.RequireModerationTarget(state);

        review.ModerationState = state;
        review.ModeratedByAdminId = request.AdminId;
        review.ModeratedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        // Rule 99: nothing to evict. The review list is public but uncached, which is
        // exactly what makes an approval visible on the next read rather than in
        // fifteen minutes.
        _logger.LogInformation(
            "Admin {AdminId} set review {ReviewId} to {State}", request.AdminId, review.Id, state);

        return _mapper.Map<AdminReviewDto>(review);
    }
}
