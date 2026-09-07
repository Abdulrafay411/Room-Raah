using System.Reflection;
using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Search;

/// <summary>
/// What a stranger is shown. Rules 79 and 84 are enforced in the mapping profile
/// rather than at each call site, so this is where they are worth testing.
/// </summary>
public class PublicListingMappingTests
{
    private readonly IMapper _mapper = TestMapper.Create();

    private static Property Listing(List<PropertyPhoto>? photos = null)
    {
        var listing = TestData.ReviewableProperty(1, status: PropertyStatus.Published,
            photos: photos ?? TestData.Photos(3));

        return listing;
    }

    [Fact]
    public void Every_public_profile_is_valid()
    {
        // AutoMapper only checks a configuration when it is asked to. A member left
        // unmapped by a rename is otherwise a silent null in a response.
        _mapper.ConfigurationProvider.AssertConfigurationIsValid();
    }

    [Fact]
    public void A_card_carries_the_pin_so_the_map_view_needs_no_second_request()
    {
        var card = _mapper.Map<ListingCardDto>(Listing());

        card.Latitude.ShouldBe(31.5204);
        card.Longitude.ShouldBe(74.3587);
    }

    [Fact]
    public void A_card_carries_the_whole_cost_so_two_of_them_can_be_compared()
    {
        var card = _mapper.Map<ListingCardDto>(Listing());

        card.MonthlyRent.ShouldBe(18000m);
        card.SecurityDeposit.ShouldBe(18000m);
        card.UtilitiesCharge.ShouldBe(2500m);
        card.MessCharge.ShouldBe(8000m);
    }

    [Fact]
    public void A_card_names_the_area_and_the_city()
    {
        var card = _mapper.Map<ListingCardDto>(Listing());

        card.AreaName.ShouldBe("Gulberg");
        card.CityName.ShouldBe("Lahore");
        card.CityId.ShouldBe(1);
    }

    [Fact]
    public void Rule_79_a_card_counts_and_illustrates_itself_from_the_public_gallery()
    {
        var listing = Listing(
        [
            TestData.Photo(1, 1, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, 1, sortOrder: 2, status: PhotoStatus.PendingRemove),
            TestData.Photo(3, 1, sortOrder: 3, status: PhotoStatus.PendingAdd)
        ]);

        var card = _mapper.Map<ListingCardDto>(listing);

        card.PhotoCount.ShouldBe(2);
        card.PrimaryThumbnailUrl.ShouldBe("http://blobs.local/properties/1/thumb1.jpg");
    }

    /// <summary>
    /// A photo the owner is trying to remove is still the cover until an admin
    /// agrees, and a card that fell back to a proposed photo would publish it.
    /// </summary>
    [Fact]
    public void Rule_79_a_primary_photo_on_its_way_out_is_still_the_cover()
    {
        var listing = Listing(
        [
            TestData.Photo(1, 1, isPrimary: true, sortOrder: 1, status: PhotoStatus.PendingRemove),
            TestData.Photo(2, 1, sortOrder: 2, status: PhotoStatus.PendingAdd)
        ]);

        var card = _mapper.Map<ListingCardDto>(listing);

        card.PrimaryThumbnailUrl.ShouldBe("http://blobs.local/properties/1/thumb1.jpg");
        card.PhotoCount.ShouldBe(1);
    }

    [Fact]
    public void Rule_79_the_detail_gallery_leaves_out_what_has_not_been_reviewed()
    {
        var listing = Listing(
        [
            TestData.Photo(1, 1, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, 1, sortOrder: 2, status: PhotoStatus.PendingRemove),
            TestData.Photo(3, 1, sortOrder: 3, status: PhotoStatus.PendingAdd)
        ]);

        var detail = _mapper.Map<ListingDetailDto>(listing);

        detail.Photos.Select(p => p.Id).ShouldBe([1, 2]);
    }

    /// <summary>
    /// "This owner is trying to delete this photograph" is not the public's business,
    /// and a status field on the public shape is the one way it could leak.
    /// </summary>
    [Fact]
    public void Rule_79_a_public_photo_has_no_status_to_read()
    {
        typeof(ListingPhotoDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ShouldNotContain("Status");
    }

    [Fact]
    public void Rule_84_the_owner_is_a_name_and_a_check()
    {
        var detail = _mapper.Map<ListingDetailDto>(Listing());

        detail.Owner.DisplayName.ShouldBe("Test Person");
        detail.Owner.IdentityVerified.ShouldBeTrue();
    }

    [Fact]
    public void Rule_84_an_unchecked_owner_is_reported_as_unchecked()
    {
        var listing = Listing();
        listing.VerificationChecks.Clear();

        var detail = _mapper.Map<ListingDetailDto>(listing);

        detail.Owner.IdentityVerified.ShouldBeFalse();
        detail.Owner.IdentityCheckedAt.ShouldBeNull();
    }

    [Fact]
    public void Rule_84_the_owner_shape_carries_nothing_else()
    {
        typeof(ListingOwnerDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ShouldBe(["DisplayName", "IdentityVerified", "IdentityCheckedAt"], ignoreOrder: true);
    }

    /// <summary>
    /// The note on a check is an internal remark about a person's documents, and the
    /// admin who recorded it is of no use to a visitor.
    /// </summary>
    [Fact]
    public void Rule_84_a_public_check_carries_no_note_and_no_admin()
    {
        var names = typeof(ListingCheckDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        names.ShouldNotContain("Note");
        names.ShouldNotContain("ReviewedByAdminId");
        names.ShouldBe(["CheckType", "Status", "EvidenceDate"], ignoreOrder: true);
    }

    [Fact]
    public void The_detail_page_carries_the_checks_a_visitor_is_told_about()
    {
        var detail = _mapper.Map<ListingDetailDto>(Listing());

        detail.Checks.ShouldNotBeEmpty();
        detail.Checks.ShouldAllBe(c => c.Status == nameof(VerificationStatus.Checked));
    }

    [Fact]
    public void The_detail_page_carries_the_address_because_section_2_lists_it()
    {
        _mapper.Map<ListingDetailDto>(Listing()).AddressLine
            .ShouldBe("House 42, Block C, Gulberg III");
    }

    [Fact]
    public void A_comparison_carries_the_facilities_it_compares()
    {
        var listing = Listing();
        listing.Facilities.Add(new PropertyFacility
        {
            PropertyId = 1,
            FacilityId = 1,
            Facility = TestData.Facility(1)
        });

        _mapper.Map<ListingComparisonDto>(listing).Facilities.Single().Name.ShouldBe("Wi-Fi");
    }

    /// <summary>
    /// The public shapes are deliberately not derived from the owner or admin ones.
    /// Inheriting one would mean a field added to an admin panel arriving on a public
    /// page without anybody deciding that it should.
    /// </summary>
    [Fact]
    public void No_public_shape_inherits_from_an_owner_or_admin_shape()
    {
        Type[] publicShapes =
        [
            typeof(ListingCardDto), typeof(ListingDetailDto),
            typeof(ListingComparisonDto), typeof(ListingPhotoDto)
        ];

        foreach (var shape in publicShapes)
        {
            var ancestors = new List<Type>();
            for (var t = shape.BaseType; t is not null && t != typeof(object); t = t.BaseType)
            {
                ancestors.Add(t);
            }

            ancestors.ShouldAllBe(a => a.Name.StartsWith("Listing"));
        }
    }
}
