using System.Reflection;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

/// <summary>
/// What the shapes of this area are allowed to say about the people in it. Rule 33
/// covers the owner and is tested over every DTO elsewhere; rules 93 and 95 are the
/// same idea pointed at the seeker and at a review's author, and they are the first
/// place in the product where one user's details are handed to another.
/// </summary>
public class SeekerShapeTests
{
    private static string[] Names<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToArray();

    /// <summary>
    /// Rule 93. The owner is not named at all: a seeker reading their own visit
    /// request learns what was said about the room, not who said it, because
    /// everything after that happens on the site.
    /// </summary>
    [Fact]
    public void Rule_93_a_visit_request_names_the_seeker_and_not_the_owner()
    {
        var names = Names<VisitRequestDto>();

        names.ShouldContain(nameof(VisitRequestDto.SeekerDisplayName));
        names.ShouldNotContain(n => n.Contains("Owner", StringComparison.OrdinalIgnoreCase) &&
                                    n != nameof(VisitRequestDto.OwnerResponseNote));
        names.ShouldNotContain(n => n.Contains("Email", StringComparison.OrdinalIgnoreCase));
        names.ShouldNotContain(n => n.Contains("SeekerId", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Rule 95. No author id, and no moderation state: the public shape is only ever
    /// built from approved reviews, and a state on it would invite a caller to filter
    /// rather than trust that the list is already filtered.
    /// </summary>
    [Fact]
    public void Rule_95_a_public_review_is_a_display_name_and_what_was_written()
    {
        Names<ReviewDto>().ShouldBe([
            nameof(ReviewDto.Id),
            nameof(ReviewDto.PropertyId),
            nameof(ReviewDto.AuthorDisplayName),
            nameof(ReviewDto.Rating),
            nameof(ReviewDto.Comment),
            nameof(ReviewDto.StayStatus),
            nameof(ReviewDto.CreatedAt)
        ], ignoreOrder: true);
    }

    /// <summary>
    /// The safe direction of inheritance. An admin shape may grow out of a public one;
    /// a public one growing out of an admin shape would gain the admin panel's fields
    /// without anybody deciding that it should.
    /// </summary>
    [Fact]
    public void An_admin_review_extends_the_public_one_and_not_the_other_way_round()
    {
        typeof(AdminReviewDto).BaseType.ShouldBe(typeof(ReviewDto));
        typeof(ReviewDto).BaseType.ShouldBe(typeof(object));
    }

    [Fact]
    public void A_reporter_is_not_told_what_the_admin_wrote()
    {
        var names = Names<MyReportDto>();

        names.ShouldNotContain(n => n.Contains("Admin", StringComparison.OrdinalIgnoreCase));
        names.ShouldNotContain(n => n.Contains("Resolved", StringComparison.OrdinalIgnoreCase));

        typeof(ReportDto).BaseType.ShouldBe(typeof(MyReportDto));
    }

    /// <summary>
    /// A shortlist card is the public card. Inheriting it is safe in this direction,
    /// and the assertion is here so that it stays the public one.
    /// </summary>
    [Fact]
    public void A_saved_listing_is_a_public_card_with_a_date_on_it()
    {
        typeof(SavedListingDto).BaseType.ShouldBe(typeof(ListingCardDto));

        Names<SavedListingDto>().Except(Names<ListingCardDto>())
            .ShouldBe([nameof(SavedListingDto.SavedAt)]);
    }

    [Fact]
    public void Every_enumeration_this_phase_added_travels_as_a_string()
    {
        var mapper = TestMapper.Create();

        var visit = mapper.Map<VisitRequestDto>(
            TestData.Visit(visitType: VisitType.Video, status: VisitStatus.Accepted));

        visit.VisitType.ShouldBe(nameof(VisitType.Video));
        visit.Status.ShouldBe(nameof(VisitStatus.Accepted));

        var review = mapper.Map<ReviewDto>(
            TestData.Review(stayStatus: StayStatus.PreviouslyStayed));

        review.StayStatus.ShouldBe(nameof(StayStatus.PreviouslyStayed));

        var report = mapper.Map<ReportDto>(
            TestData.Report(reason: ReportReason.Scam, status: ReportStatus.Dismissed));

        report.Reason.ShouldBe(nameof(ReportReason.Scam));
        report.Status.ShouldBe(nameof(ReportStatus.Dismissed));
    }
}
