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

namespace RoomRaah.Application.Features.Reviews.Commands;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, MyReviewDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IReviewRepository _reviews;
    private readonly IVisitRequestRepository _visits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateReviewCommandHandler> _logger;

    public CreateReviewCommandHandler(
        IPropertyRepository properties,
        IReviewRepository reviews,
        IVisitRequestRepository visits,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateReviewCommandHandler> logger)
    {
        _properties = properties;
        _reviews = reviews;
        _visits = visits;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<MyReviewDto> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        // Rule 86.
        var property = await _properties.GetPublishedWithOwnerAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        // Rule 39 before rule 38: a second review is the likelier mistake, and asking
        // about it first means the answer is "you have already written one" rather
        // than a puzzling remark about visits.
        if (await _reviews.ExistsForAsync(request.AuthorId, property.Id))
        {
            throw new BusinessRuleException(SeekerMessages.AlreadyReviewed);
        }

        // Rule 38. This is what the whole visit lifecycle is in aid of: a review here
        // was written by someone who arranged to see the room and then said they had.
        if (!await _visits.HasCompletedAsync(request.AuthorId, property.Id))
        {
            throw new BusinessRuleException(SeekerMessages.ReviewNeedsCompletedVisit);
        }

        var review = new Review
        {
            PropertyId = property.Id,
            AuthorId = request.AuthorId,
            Rating = request.Rating,
            Comment = request.Comment.Trim(),
            StayStatus = Enum.Parse<StayStatus>(request.StayStatus, ignoreCase: true),

            // Rule 41. Set here rather than left to the entity default, because the
            // one thing this line must never be is implicit.
            ModerationState = ModerationState.Pending
        };

        await _reviews.CreateAsync(review);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Seeker {AuthorId} reviewed property {PropertyId} and it is waiting for moderation",
            request.AuthorId, property.Id);

        return _mapper.Map<MyReviewDto>(review);
    }
}
