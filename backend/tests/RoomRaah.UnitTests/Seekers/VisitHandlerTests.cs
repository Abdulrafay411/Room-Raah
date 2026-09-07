using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Features.Visits.Commands;
using RoomRaah.Application.Features.Visits.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

public class VisitHandlerTests
{
    private const int OwnerId = 1;
    private const int SeekerId = 2;
    private const int Stranger = 99;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IVisitRequestRepository _visits = Substitute.For<IVisitRequestRepository>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = TestMapper.Create();

    private CreateVisitRequestCommandHandler Create() =>
        new(_properties, _visits, _email, _unitOfWork, _mapper,
            NullLogger<CreateVisitRequestCommandHandler>.Instance);

    private RespondToVisitCommandHandler Respond() =>
        new(_visits, _email, _unitOfWork, _mapper, NullLogger<RespondToVisitCommandHandler>.Instance);

    private CompleteVisitCommandHandler Complete() =>
        new(_visits, _unitOfWork, _mapper, NullLogger<CompleteVisitCommandHandler>.Instance);

    private CancelVisitCommandHandler Cancel() =>
        new(_visits, _unitOfWork, _mapper, NullLogger<CancelVisitCommandHandler>.Instance);

    private CreateVisitRequestCommand NewRequest() => new()
    {
        SeekerId = SeekerId,
        PropertyId = 1,
        VisitType = "Physical",
        PreferredAt = DateTime.UtcNow.AddDays(3)
    };

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task A_visit_request_is_created_waiting_for_the_owner()
    {
        var property = TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published);
        _properties.GetPublishedWithOwnerAsync(1).Returns(property);
        _visits.HasOpenAsync(SeekerId, 1).Returns(false);
        _visits.GetByIdAsync(Arg.Any<int>()).Returns(TestData.Visit(seekerId: SeekerId, ownerId: OwnerId));

        var dto = await Create().Handle(NewRequest(), default);

        dto.Status.ShouldBe(nameof(VisitStatus.Requested));

        await _visits.Received(1).CreateAsync(Arg.Is<VisitRequest>(v =>
            v.Status == VisitStatus.Requested &&
            v.SeekerId == SeekerId &&
            v.VisitType == VisitType.Physical));
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    /// <summary>
    /// Rule 92, and the reason it exists: there is no phone number in this product,
    /// so a request nobody is told about is a request nobody answers.
    /// </summary>
    [Fact]
    public async Task Rule_92_the_owner_is_emailed_when_a_visit_is_requested()
    {
        var property = TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published);
        _properties.GetPublishedWithOwnerAsync(1).Returns(property);
        _visits.HasOpenAsync(SeekerId, 1).Returns(false);
        _visits.GetByIdAsync(Arg.Any<int>()).Returns(TestData.Visit(seekerId: SeekerId, ownerId: OwnerId));

        await Create().Handle(NewRequest(), default);

        await _email.Received(1).SendVisitRequestedAsync(
            Arg.Is<AppUser>(u => u.Id == OwnerId),
            property.Title,
            VisitType.Physical,
            Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Rule_86_a_visit_cannot_be_requested_on_a_listing_that_is_not_published()
    {
        _properties.GetPublishedWithOwnerAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(Create().Handle(NewRequest(), default));

        await _visits.DidNotReceive().CreateAsync(Arg.Any<VisitRequest>());
        await _email.DidNotReceive().SendVisitRequestedAsync(
            Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<VisitType>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Rule_36_a_second_open_request_on_the_same_listing_is_refused()
    {
        _properties.GetPublishedWithOwnerAsync(1).Returns(
            TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published));
        _visits.HasOpenAsync(SeekerId, 1).Returns(true);

        await Should.ThrowAsync<BusinessRuleException>(Create().Handle(NewRequest(), default));

        await _visits.DidNotReceive().CreateAsync(Arg.Any<VisitRequest>());
    }

    // ----------------------------------------------------------------- respond

    [Fact]
    public async Task An_owner_accepts_a_request_on_their_own_listing()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(5, seekerId: SeekerId, ownerId: OwnerId));

        var dto = await Respond().Handle(new RespondToVisitCommand
        {
            VisitId = 5,
            OwnerId = OwnerId,
            Status = "Accepted",
            OwnerResponseNote = "Come after 4pm."
        }, default);

        dto.Status.ShouldBe(nameof(VisitStatus.Accepted));
        dto.OwnerResponseNote.ShouldBe("Come after 4pm.");
        dto.RespondedAt.ShouldNotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_37_only_the_listings_owner_may_answer()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(5, seekerId: SeekerId, ownerId: OwnerId));

        await Should.ThrowAsync<UnauthorizedAccessException>(Respond().Handle(new RespondToVisitCommand
        {
            VisitId = 5,
            OwnerId = Stranger,
            Status = "Accepted"
        }, default));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_88_a_request_that_has_already_been_answered_is_not_answered_again()
    {
        _visits.GetByIdAsync(5).Returns(
            TestData.Visit(5, seekerId: SeekerId, ownerId: OwnerId, status: VisitStatus.Accepted));

        await Should.ThrowAsync<BusinessRuleException>(Respond().Handle(new RespondToVisitCommand
        {
            VisitId = 5,
            OwnerId = OwnerId,
            Status = "Declined",
            OwnerResponseNote = "Taken."
        }, default));
    }

    [Fact]
    public async Task Rule_89_a_decline_without_a_reason_is_refused()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(5, seekerId: SeekerId, ownerId: OwnerId));

        await Should.ThrowAsync<ValidationException>(Respond().Handle(new RespondToVisitCommand
        {
            VisitId = 5,
            OwnerId = OwnerId,
            Status = "Declined"
        }, default));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_92_the_seeker_is_emailed_with_the_answer_and_the_note()
    {
        var visit = TestData.Visit(5, seekerId: SeekerId, ownerId: OwnerId);
        _visits.GetByIdAsync(5).Returns(visit);

        await Respond().Handle(new RespondToVisitCommand
        {
            VisitId = 5,
            OwnerId = OwnerId,
            Status = "Declined",
            OwnerResponseNote = "Both beds are taken."
        }, default);

        await _email.Received(1).SendVisitResponseAsync(
            Arg.Is<AppUser>(u => u.Id == SeekerId),
            visit.Property.Title,
            nameof(VisitStatus.Declined),
            "Both beds are taken.");
    }

    // ---------------------------------------------------------------- complete

    [Theory]
    [InlineData(SeekerId)]
    [InlineData(OwnerId)]
    public async Task Rule_90_either_participant_may_say_the_visit_happened(int actor)
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(
            5, seekerId: SeekerId, ownerId: OwnerId,
            status: VisitStatus.Accepted, preferredAt: DateTime.UtcNow.AddDays(-1)));

        var dto = await Complete().Handle(
            new CompleteVisitCommand { VisitId = 5, UserId = actor }, default);

        dto.Status.ShouldBe(nameof(VisitStatus.Completed));
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_90_somebody_who_was_not_there_may_not_complete_it()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(
            5, seekerId: SeekerId, ownerId: OwnerId,
            status: VisitStatus.Accepted, preferredAt: DateTime.UtcNow.AddDays(-1)));

        await Should.ThrowAsync<UnauthorizedAccessException>(Complete().Handle(
            new CompleteVisitCommand { VisitId = 5, UserId = Stranger }, default));
    }

    [Fact]
    public async Task Rule_90_a_visit_still_in_the_future_cannot_be_completed()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(
            5, seekerId: SeekerId, ownerId: OwnerId,
            status: VisitStatus.Accepted, preferredAt: DateTime.UtcNow.AddDays(2)));

        await Should.ThrowAsync<BusinessRuleException>(Complete().Handle(
            new CompleteVisitCommand { VisitId = 5, UserId = SeekerId }, default));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_90_a_request_the_owner_never_accepted_cannot_be_completed()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(
            5, seekerId: SeekerId, ownerId: OwnerId,
            status: VisitStatus.Requested, preferredAt: DateTime.UtcNow.AddDays(-1)));

        await Should.ThrowAsync<BusinessRuleException>(Complete().Handle(
            new CompleteVisitCommand { VisitId = 5, UserId = SeekerId }, default));
    }

    // ------------------------------------------------------------------ cancel

    [Fact]
    public async Task Rule_91_the_seeker_withdraws_their_own_request()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(5, seekerId: SeekerId, ownerId: OwnerId));

        var dto = await Cancel().Handle(
            new CancelVisitCommand { VisitId = 5, SeekerId = SeekerId }, default);

        dto.Status.ShouldBe(nameof(VisitStatus.Cancelled));
    }

    /// <summary>
    /// Rule 91. An owner who does not want a visit declines it, which says so and
    /// carries a reason; cancelling it would look to the seeker like their own doing.
    /// </summary>
    [Fact]
    public async Task Rule_91_the_owner_may_not_cancel_a_request()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(5, seekerId: SeekerId, ownerId: OwnerId));

        await Should.ThrowAsync<UnauthorizedAccessException>(Cancel().Handle(
            new CancelVisitCommand { VisitId = 5, SeekerId = OwnerId }, default));
    }

    [Fact]
    public async Task Rule_91_a_finished_request_cannot_be_cancelled()
    {
        _visits.GetByIdAsync(5).Returns(TestData.Visit(
            5, seekerId: SeekerId, ownerId: OwnerId, status: VisitStatus.Completed));

        await Should.ThrowAsync<BusinessRuleException>(Cancel().Handle(
            new CancelVisitCommand { VisitId = 5, SeekerId = SeekerId }, default));
    }

    [Fact]
    public async Task A_missing_visit_request_is_not_found()
    {
        _visits.GetByIdAsync(5).Returns((VisitRequest?)null);

        await Should.ThrowAsync<NotFoundException>(Cancel().Handle(
            new CancelVisitCommand { VisitId = 5, SeekerId = SeekerId }, default));
    }

    // ------------------------------------------------------------------- lists

    [Fact]
    public async Task A_seeker_reads_their_own_requests_and_an_owner_reads_the_ones_on_their_listings()
    {
        _visits.GetForSeekerAsync(SeekerId, 1, 12).Returns([TestData.Visit(1, seekerId: SeekerId)]);
        _visits.CountForSeekerAsync(SeekerId).Returns(1);
        _visits.GetForOwnerAsync(OwnerId, 1, 12).Returns([
            TestData.Visit(1, ownerId: OwnerId), TestData.Visit(2, ownerId: OwnerId)
        ]);
        _visits.CountForOwnerAsync(OwnerId).Returns(2);

        var mine = await new GetMyVisitsQueryHandler(_visits, _mapper).Handle(
            new GetMyVisitsQuery { SeekerId = SeekerId }, default);

        var theirs = await new GetOwnerVisitsQueryHandler(_visits, _mapper).Handle(
            new GetOwnerVisitsQuery { OwnerId = OwnerId }, default);

        mine.TotalCount.ShouldBe(1);
        theirs.TotalCount.ShouldBe(2);
        theirs.Items.ShouldAllBe(i => i.SeekerDisplayName != string.Empty);
    }
}
