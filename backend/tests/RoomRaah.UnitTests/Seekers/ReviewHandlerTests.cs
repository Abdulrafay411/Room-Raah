using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Features.Reviews.Commands;
using RoomRaah.Application.Features.Reviews.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

public class ReviewHandlerTests
{
    private const int AuthorId = 2;
    private const int AdminId = 9;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();
    private readonly IVisitRequestRepository _visits = Substitute.For<IVisitRequestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = TestMapper.Create();

    private CreateReviewCommandHandler Create() =>
        new(_properties, _reviews, _visits, _unitOfWork, _mapper,
            NullLogger<CreateReviewCommandHandler>.Instance);

    private ModerateReviewCommandHandler Moderate() =>
        new(_reviews, _unitOfWork, _mapper, NullLogger<ModerateReviewCommandHandler>.Instance);

    private GetPropertyReviewsQueryHandler Read() => new(_properties, _reviews, _mapper);

    private static CreateReviewCommand NewReview() => new()
    {
        AuthorId = AuthorId,
        PropertyId = 1,
        Rating = 4,
        Comment = "  Clean room and the mess food is good.  ",
        StayStatus = "CurrentlyStaying"
    };

    private void ListingExists() =>
        _properties.GetPublishedWithOwnerAsync(1).Returns(
            TestData.ReviewableProperty(1, status: PropertyStatus.Published));

    // ------------------------------------------------------------------ create

    /// <summary>Rule 41: a review starts invisible, and the author is told so.</summary>
    [Fact]
    public async Task Rule_41_a_review_is_created_waiting_for_moderation()
    {
        ListingExists();
        _reviews.ExistsForAsync(AuthorId, 1).Returns(false);
        _visits.HasCompletedAsync(AuthorId, 1).Returns(true);

        var dto = await Create().Handle(NewReview(), default);

        dto.ModerationState.ShouldBe(nameof(ModerationState.Pending));
        dto.Comment.ShouldBe("Clean room and the mess food is good.");
        dto.StayStatus.ShouldBe(nameof(StayStatus.CurrentlyStaying));

        await _reviews.Received(1).CreateAsync(Arg.Is<Review>(r =>
            r.ModerationState == ModerationState.Pending && r.Rating == 4));
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    /// <summary>
    /// Rule 38, and the reason the whole visit lifecycle exists: a review here was
    /// written by someone who arranged to see the room and then said they had.
    /// </summary>
    [Fact]
    public async Task Rule_38_a_review_without_a_completed_visit_is_refused()
    {
        ListingExists();
        _reviews.ExistsForAsync(AuthorId, 1).Returns(false);
        _visits.HasCompletedAsync(AuthorId, 1).Returns(false);

        await Should.ThrowAsync<BusinessRuleException>(Create().Handle(NewReview(), default));

        await _reviews.DidNotReceive().CreateAsync(Arg.Any<Review>());
    }

    [Fact]
    public async Task Rule_39_a_second_review_of_the_same_listing_is_refused()
    {
        ListingExists();
        _reviews.ExistsForAsync(AuthorId, 1).Returns(true);

        await Should.ThrowAsync<BusinessRuleException>(Create().Handle(NewReview(), default));

        await _reviews.DidNotReceive().CreateAsync(Arg.Any<Review>());
    }

    [Fact]
    public async Task Rule_86_a_listing_that_is_not_published_cannot_be_reviewed()
    {
        _properties.GetPublishedWithOwnerAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(Create().Handle(NewReview(), default));
    }

    // ---------------------------------------------------------------- moderate

    [Fact]
    public async Task An_admin_approves_a_review_and_the_decision_is_stamped()
    {
        _reviews.GetByIdAsync(3).Returns(TestData.Review(3));

        var dto = await Moderate().Handle(new ModerateReviewCommand
        {
            ReviewId = 3,
            AdminId = AdminId,
            State = "Approved"
        }, default);

        dto.ModerationState.ShouldBe(nameof(ModerationState.Approved));
        dto.ModeratedAt.ShouldNotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_96_a_review_cannot_be_moved_back_to_pending()
    {
        _reviews.GetByIdAsync(3).Returns(TestData.Review(3, state: ModerationState.Approved));

        await Should.ThrowAsync<BusinessRuleException>(Moderate().Handle(new ModerateReviewCommand
        {
            ReviewId = 3,
            AdminId = AdminId,
            State = "Pending"
        }, default));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task A_missing_review_is_not_found()
    {
        _reviews.GetByIdAsync(3).Returns((Review?)null);

        await Should.ThrowAsync<NotFoundException>(Moderate().Handle(new ModerateReviewCommand
        {
            ReviewId = 3,
            AdminId = AdminId,
            State = "Hidden"
        }, default));
    }

    // -------------------------------------------------------------------- read

    [Fact]
    public async Task Rule_94_the_public_list_carries_the_average_and_the_count()
    {
        ListingExists();
        _reviews.GetApprovedAsync(1, 1, 12).Returns([
            TestData.Review(1, state: ModerationState.Approved, rating: 5),
            TestData.Review(2, authorId: 3, state: ModerationState.Approved, rating: 3)
        ]);
        _reviews.CountApprovedAsync(1).Returns(2);
        _reviews.AverageApprovedAsync(1).Returns(4d);

        var page = await Read().Handle(new GetPropertyReviewsQuery { PropertyId = 1 }, default);

        page.TotalCount.ShouldBe(2);
        page.AverageRating.ShouldBe(4d);
        page.Items.Count.ShouldBe(2);
        page.Items.ShouldAllBe(r => r.AuthorDisplayName != string.Empty);
    }

    /// <summary>A listing nobody has rated does not have a rating of zero.</summary>
    [Fact]
    public async Task A_listing_with_no_approved_reviews_has_no_average()
    {
        ListingExists();
        _reviews.GetApprovedAsync(1, 1, 12).Returns([]);
        _reviews.CountApprovedAsync(1).Returns(0);
        _reviews.AverageApprovedAsync(1).Returns((double?)null);

        var page = await Read().Handle(new GetPropertyReviewsQuery { PropertyId = 1 }, default);

        page.AverageRating.ShouldBeNull();
        page.Items.ShouldBeEmpty();
    }

    /// <summary>
    /// Rule 28 is asked of the listing before its reviews are read, so the reviews of
    /// a listing an admin has taken down are not readable through a route that never
    /// looks at the listing itself.
    /// </summary>
    [Fact]
    public async Task Rule_28_the_reviews_of_an_unpublished_listing_are_not_found()
    {
        _properties.GetPublishedWithOwnerAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(
            Read().Handle(new GetPropertyReviewsQuery { PropertyId = 1 }, default));

        await _reviews.DidNotReceive().GetApprovedAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
}
