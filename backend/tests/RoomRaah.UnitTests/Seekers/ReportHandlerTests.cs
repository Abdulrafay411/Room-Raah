using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Features.Reports.Commands;
using RoomRaah.Application.Features.Reports.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

public class ReportHandlerTests
{
    private const int UserId = 2;
    private const int AdminId = 9;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IReportRepository _reports = Substitute.For<IReportRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = TestMapper.Create();

    private CreateReportCommandHandler Create() =>
        new(_properties, _reports, _unitOfWork, _mapper, NullLogger<CreateReportCommandHandler>.Instance);

    private ResolveReportCommandHandler Resolve() =>
        new(_reports, _unitOfWork, _mapper, NullLogger<ResolveReportCommandHandler>.Instance);

    private static CreateReportCommand NewReport() => new()
    {
        ReportedByUserId = UserId,
        PropertyId = 1,
        Reason = "AlreadyTaken",
        Details = "  The room went a month ago.  "
    };

    private void ListingExists() =>
        _properties.GetPublishedWithOwnerAsync(1).Returns(
            TestData.ReviewableProperty(1, status: PropertyStatus.Published));

    [Fact]
    public async Task A_report_is_created_open()
    {
        ListingExists();
        _reports.HasUnresolvedAsync(UserId, 1).Returns(false);

        var dto = await Create().Handle(NewReport(), default);

        dto.Status.ShouldBe(nameof(ReportStatus.Open));
        dto.Reason.ShouldBe(nameof(ReportReason.AlreadyTaken));
        dto.Details.ShouldBe("The room went a month ago.");

        await _reports.Received(1).CreateAsync(Arg.Is<Report>(r => r.Status == ReportStatus.Open));
    }

    [Fact]
    public async Task Rule_86_a_listing_that_is_not_published_cannot_be_reported()
    {
        _properties.GetPublishedWithOwnerAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(Create().Handle(NewReport(), default));

        await _reports.DidNotReceive().CreateAsync(Arg.Any<Report>());
    }

    /// <summary>
    /// Rule 42 as amended in this phase. A second report from the same person about
    /// the same listing while the first is still being read is the same complaint
    /// twice, and it pushes a real one down the queue.
    /// </summary>
    [Fact]
    public async Task Rule_42_a_second_unresolved_report_is_refused()
    {
        ListingExists();
        _reports.HasUnresolvedAsync(UserId, 1).Returns(true);

        await Should.ThrowAsync<BusinessRuleException>(Create().Handle(NewReport(), default));

        await _reports.DidNotReceive().CreateAsync(Arg.Any<Report>());
    }

    [Fact]
    public async Task An_admin_upholds_a_report_and_the_verdict_is_stamped()
    {
        _reports.GetByIdAsync(4).Returns(TestData.Report(4));

        var dto = await Resolve().Handle(new ResolveReportCommand
        {
            ReportId = 4,
            AdminId = AdminId,
            Status = "Upheld",
            AdminNote = "Confirmed with the owner."
        }, default);

        dto.Status.ShouldBe(nameof(ReportStatus.Upheld));
        dto.AdminNote.ShouldBe("Confirmed with the owner.");
        dto.ResolvedByAdminId.ShouldBe(AdminId);
        dto.ResolvedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Rule 98. A listing comes down because an admin decided to take it down, on the
    /// admin listing endpoints, and not as a side effect of answering a complaint.
    /// </summary>
    [Fact]
    public async Task Rule_98_upholding_a_report_leaves_the_listing_where_it_is()
    {
        var report = TestData.Report(4);
        _reports.GetByIdAsync(4).Returns(report);

        await Resolve().Handle(new ResolveReportCommand
        {
            ReportId = 4,
            AdminId = AdminId,
            Status = "Upheld",
            AdminNote = "Confirmed."
        }, default);

        report.Property.Status.ShouldBe(PropertyStatus.Published);
        report.Property.IsActive.ShouldBeTrue();
    }

    /// <summary>Moving a report under review is not a verdict, so nothing is stamped.</summary>
    [Fact]
    public async Task Picking_a_report_up_is_not_resolving_it()
    {
        _reports.GetByIdAsync(4).Returns(TestData.Report(4));

        var dto = await Resolve().Handle(new ResolveReportCommand
        {
            ReportId = 4,
            AdminId = AdminId,
            Status = "UnderReview"
        }, default);

        dto.Status.ShouldBe(nameof(ReportStatus.UnderReview));
        dto.ResolvedByAdminId.ShouldBeNull();
        dto.ResolvedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(ReportStatus.Upheld)]
    [InlineData(ReportStatus.Dismissed)]
    public async Task Rule_97_a_report_that_already_carries_a_verdict_is_not_decided_again(
        ReportStatus current)
    {
        _reports.GetByIdAsync(4).Returns(TestData.Report(4, status: current));

        await Should.ThrowAsync<BusinessRuleException>(Resolve().Handle(new ResolveReportCommand
        {
            ReportId = 4,
            AdminId = AdminId,
            Status = "Dismissed",
            AdminNote = "Changed my mind."
        }, default));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_97_a_verdict_without_a_note_is_refused()
    {
        _reports.GetByIdAsync(4).Returns(TestData.Report(4));

        await Should.ThrowAsync<ValidationException>(Resolve().Handle(new ResolveReportCommand
        {
            ReportId = 4,
            AdminId = AdminId,
            Status = "Dismissed"
        }, default));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task A_missing_report_is_not_found()
    {
        _reports.GetByIdAsync(4).Returns((Report?)null);

        await Should.ThrowAsync<NotFoundException>(Resolve().Handle(new ResolveReportCommand
        {
            ReportId = 4,
            AdminId = AdminId,
            Status = "UnderReview"
        }, default));
    }

    [Fact]
    public async Task The_queue_is_read_in_one_status_at_a_time()
    {
        _reports.GetQueueAsync(ReportStatus.Open, 1, 12).Returns([TestData.Report(1)]);
        _reports.CountQueueAsync(ReportStatus.Open).Returns(1);

        var page = await new GetReportQueueQueryHandler(_reports, _mapper).Handle(
            new GetReportQueueQuery { Status = "Open" }, default);

        page.TotalCount.ShouldBe(1);
        page.Items.Single().ReportedByDisplayName.ShouldNotBeNullOrWhiteSpace();
        page.Items.Single().PropertyTitle.ShouldNotBeNullOrWhiteSpace();
    }
}
