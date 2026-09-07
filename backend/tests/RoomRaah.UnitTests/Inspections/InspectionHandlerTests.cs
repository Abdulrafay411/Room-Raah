using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.Features.Inspections.Commands;
using RoomRaah.Application.Features.Inspections.Queries;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Inspections;

public class InspectionHandlerTests
{
    private const int OwnerId = 1;
    private const int InspectorId = 7;
    private const int PlainAdminId = 8;
    private const int Stranger = 99;

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IInspectionRepository _inspections = Substitute.For<IInspectionRepository>();
    private readonly IBadgeEventRepository _events = Substitute.For<IBadgeEventRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = TestMapper.Create();

    public InspectionHandlerTests()
    {
        _users.GetByIdAsync(InspectorId).Returns(Admin(InspectorId, canInspect: true));
        _users.GetByIdAsync(PlainAdminId).Returns(Admin(PlainAdminId, canInspect: false));
    }

    private static AppUser Admin(int id, bool canInspect)
    {
        var admin = TestData.User(id, $"admin{id}@roomraah.local", UserRole.Admin);

        admin.AdminProfile = new AdminProfile { AppUserId = id, CanInspect = canInspect };

        return admin;
    }

    private RequestInspectionCommandHandler Request() =>
        new(_properties, _unitOfWork, _mapper,
            NullLogger<RequestInspectionCommandHandler>.Instance);

    private RecordInspectionCommandHandler Record() =>
        new(_users, _properties, _inspections, _unitOfWork, _mapper,
            NullLogger<RecordInspectionCommandHandler>.Instance);

    private GrantBadgeCommandHandler Grant() =>
        new(_users, _properties, _inspections, _events, _cache, _unitOfWork, _mapper,
            NullLogger<GrantBadgeCommandHandler>.Instance);

    private RemoveBadgeCommandHandler Remove() =>
        new(_users, _properties, _inspections, _events, _cache, _unitOfWork, _mapper,
            NullLogger<RemoveBadgeCommandHandler>.Instance);

    private GetDueForInspectionQueryHandler Due() => new(_properties, _mapper);

    // ------------------------------------------------------- request (rule 108)

    [Fact]
    public async Task An_owner_asks_for_an_inspection_once()
    {
        var property = TestData.Property(1, OwnerId, PropertyStatus.Published);
        _properties.GetByIdAsync(1).Returns(property);

        var state = await Request().Handle(
            new RequestInspectionCommand { PropertyId = 1, OwnerId = OwnerId }, default);

        state.InspectionRequested.ShouldBeTrue();
        property.InspectionRequestedAt.ShouldNotBeNull();

        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task A_second_request_is_refused()
    {
        var property = TestData.Property(1, OwnerId, PropertyStatus.Published);
        property.InspectionRequestedAt = DateTime.UtcNow.AddDays(-3);
        _properties.GetByIdAsync(1).Returns(property);

        await Should.ThrowAsync<BusinessRuleException>(() => Request().Handle(
            new RequestInspectionCommand { PropertyId = 1, OwnerId = OwnerId }, default));
    }

    [Fact]
    public async Task Somebody_else_cannot_request_an_inspection_of_a_listing()
    {
        _properties.GetByIdAsync(1).Returns(TestData.Property(1, OwnerId, PropertyStatus.Published));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => Request().Handle(
            new RequestInspectionCommand { PropertyId = 1, OwnerId = Stranger }, default));
    }

    /// <summary>An inspection checks the property against the listing, so there has to be one.</summary>
    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.PendingReview)]
    [InlineData(PropertyStatus.Rejected)]
    [InlineData(PropertyStatus.Unpublished)]
    public async Task A_listing_that_is_not_published_cannot_be_inspected(PropertyStatus status)
    {
        _properties.GetByIdAsync(1).Returns(TestData.Property(1, OwnerId, status));

        await Should.ThrowAsync<BusinessRuleException>(() => Request().Handle(
            new RequestInspectionCommand { PropertyId = 1, OwnerId = OwnerId }, default));
    }

    // -------------------------------------------------------- record (rule 109)

    [Fact]
    public async Task An_inspector_records_a_visit_and_stamps_the_listing()
    {
        var property = TestData.Property(1, OwnerId, PropertyStatus.Published);
        _properties.GetByIdAsync(1).Returns(property);

        var at = DateTime.UtcNow.AddDays(-1);

        var dto = await Record().Handle(new RecordInspectionCommand
        {
            AdminId = InspectorId,
            PropertyId = 1,
            InspectedAt = at,
            Result = "Passed"
        }, default);

        dto.Result.ShouldBe(nameof(InspectionResult.Passed));
        property.LastInspectionAt.ShouldBe(at);

        await _inspections.Received(1).CreateAsync(Arg.Is<PropertyInspection>(i =>
            i.Result == InspectionResult.Passed && i.InspectorAdminId == InspectorId));
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    /// <summary>
    /// Rule 109. "Admin" and "admin who makes visits" are different answers, and the
    /// seeder has one of each precisely so the difference is exercised.
    /// </summary>
    [Fact]
    public async Task An_admin_who_does_not_make_visits_cannot_record_one()
    {
        await Should.ThrowAsync<UnauthorizedAccessException>(() => Record().Handle(
            new RecordInspectionCommand
            {
                AdminId = PlainAdminId,
                PropertyId = 1,
                InspectedAt = DateTime.UtcNow.AddDays(-1),
                Result = "Passed"
            }, default));

        await _inspections.DidNotReceive().CreateAsync(Arg.Any<PropertyInspection>());
    }

    /// <summary>The permission is asked before anything is read about the listing.</summary>
    [Fact]
    public async Task The_inspector_check_comes_before_the_listing_is_loaded()
    {
        await Should.ThrowAsync<UnauthorizedAccessException>(() => Record().Handle(
            new RecordInspectionCommand
            {
                AdminId = PlainAdminId,
                PropertyId = 1,
                InspectedAt = DateTime.UtcNow.AddDays(-1),
                Result = "Passed"
            }, default));

        await _properties.DidNotReceive().GetByIdAsync(Arg.Any<int>());
    }

    // ---------------------------------------------------------- grant (110-113)

    [Fact]
    public async Task The_badge_goes_on_after_a_passed_visit()
    {
        var property = TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published);
        _properties.GetForReviewAsync(1).Returns(property);
        _inspections.LatestResultAsync(1).Returns(InspectionResult.Passed);

        var dto = await Grant().Handle(new GrantBadgeCommand
        {
            PropertyId = 1,
            AdminId = InspectorId,
            Reason = "Passed the visit on the 3rd."
        }, default);

        dto.HasInspectionBadge.ShouldBeTrue();
        property.HasInspectionBadge.ShouldBeTrue();

        // Rule 112.
        await _events.Received(1).CreateAsync(Arg.Is<BadgeEvent>(e =>
            e.Action == BadgeAction.Granted &&
            e.ActedByAdminId == InspectorId &&
            e.Reason == "Passed the visit on the 3rd."));

        // Rule 113: rule 82 orders the featured list by the badge.
        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }

    [Fact]
    public async Task The_badge_is_refused_when_the_last_visit_failed()
    {
        _properties.GetForReviewAsync(1)
            .Returns(TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published));
        _inspections.LatestResultAsync(1).Returns(InspectionResult.Failed);

        await Should.ThrowAsync<BusinessRuleException>(() => Grant().Handle(
            new GrantBadgeCommand { PropertyId = 1, AdminId = InspectorId, Reason = "Because." },
            default));

        await _events.DidNotReceive().CreateAsync(Arg.Any<BadgeEvent>());
    }

    [Fact]
    public async Task An_admin_who_does_not_make_visits_cannot_grant_the_badge()
    {
        await Should.ThrowAsync<UnauthorizedAccessException>(() => Grant().Handle(
            new GrantBadgeCommand { PropertyId = 1, AdminId = PlainAdminId, Reason = "Because." },
            default));
    }

    /// <summary>
    /// Rule 112, and the order it is asked in: a caller who sends no reason is told
    /// about the missing field rather than about a rule they were never going to reach.
    /// </summary>
    [Fact]
    public async Task A_grant_with_no_reason_is_a_bad_request()
    {
        _properties.GetForReviewAsync(1)
            .Returns(TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published));

        await Should.ThrowAsync<FluentValidation.ValidationException>(() => Grant().Handle(
            new GrantBadgeCommand { PropertyId = 1, AdminId = InspectorId, Reason = " " },
            default));
    }

    // --------------------------------------------------------- remove (111-113)

    [Fact]
    public async Task Any_admin_may_take_the_badge_off()
    {
        var property = TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published);
        property.HasInspectionBadge = true;
        _properties.GetForReviewAsync(1).Returns(property);

        var dto = await Remove().Handle(new RemoveBadgeCommand
        {
            PropertyId = 1,
            AdminId = PlainAdminId,
            Reason = "A report about this listing was upheld."
        }, default);

        dto.HasInspectionBadge.ShouldBeFalse();

        await _events.Received(1).CreateAsync(Arg.Is<BadgeEvent>(e =>
            e.Action == BadgeAction.Removed));
        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }

    [Fact]
    public async Task A_badge_that_is_not_there_cannot_be_taken_off()
    {
        _properties.GetForReviewAsync(1)
            .Returns(TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published));

        await Should.ThrowAsync<BusinessRuleException>(() => Remove().Handle(
            new RemoveBadgeCommand { PropertyId = 1, AdminId = PlainAdminId, Reason = "Because." },
            default));
    }

    // ------------------------------------------------------- worklist (rule 114)

    [Fact]
    public async Task The_worklist_counts_how_far_past_the_interval_each_listing_is()
    {
        var now = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);

        var property = TestData.Property(1, OwnerId, PropertyStatus.Published);
        property.Owner = TestData.User(OwnerId, "owner@roomraah.local", UserRole.Owner);
        property.HasInspectionBadge = true;
        property.LastInspectionAt = now.AddDays(-45);

        _properties.GetDueForInspectionAsync(Arg.Any<DateTime>(), 1, 12).Returns([property]);
        _properties.CountDueForInspectionAsync(Arg.Any<DateTime>()).Returns(1);

        var page = await Due().Handle(new GetDueForInspectionQuery { AsOf = now }, default);

        page.Items.Single().DaysOverdue.ShouldBe(15);
    }

    [Fact]
    public async Task The_worklist_asks_for_listings_not_visited_in_thirty_days()
    {
        var now = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);

        _properties.GetDueForInspectionAsync(Arg.Any<DateTime>(), 1, 12).Returns([]);

        await Due().Handle(new GetDueForInspectionQuery { AsOf = now }, default);

        await _properties.Received(1).GetDueForInspectionAsync(now.AddDays(-30), 1, 12);
    }

    /// <summary>A badged listing nobody has ever visited is as overdue as it can be, not a crash.</summary>
    [Fact]
    public async Task A_badged_listing_with_no_visit_at_all_does_not_break_the_worklist()
    {
        var now = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);

        var property = TestData.Property(1, OwnerId, PropertyStatus.Published);
        property.Owner = TestData.User(OwnerId, "owner@roomraah.local", UserRole.Owner);
        property.HasInspectionBadge = true;
        property.LastInspectionAt = null;

        _properties.GetDueForInspectionAsync(Arg.Any<DateTime>(), 1, 12).Returns([property]);
        _properties.CountDueForInspectionAsync(Arg.Any<DateTime>()).Returns(1);

        var page = await Due().Handle(new GetDueForInspectionQuery { AsOf = now }, default);

        page.Items.Single().DaysOverdue.ShouldBe(0);
    }
}
