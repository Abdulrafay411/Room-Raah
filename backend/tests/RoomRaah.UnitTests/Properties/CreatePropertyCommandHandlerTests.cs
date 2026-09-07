using Microsoft.Extensions.Options;
using NSubstitute;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Properties;

public class CreatePropertyCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int AreaId = 3;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly IFacilityRepository _facilities = Substitute.For<IFacilityRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreatePropertyCommandHandler _handler;

    private Property? _created;

    public CreatePropertyCommandHandlerTests()
    {
        var owner = TestData.User(OwnerId, "owner@roomraah.local", UserRole.Owner);
        owner.OwnerProfile = new OwnerProfile { CnicNumber = "3520112345671", PayoutContact = "HBL 1" };

        _users.GetByIdAsync(OwnerId).Returns(owner);
        _locations.AreaExistsAsync(AreaId).Returns(true);

        _properties.CreateAsync(Arg.Do<Property>(p => _created = p)).Returns(Task.CompletedTask);

        // The handler reads the listing back so a create and a fetch answer in the
        // same shape. The real repository includes the area and its city on that
        // read, so the substitute attaches them too — otherwise the mapping that
        // fills in the area and city names is never exercised.
        _properties.GetByIdAsync(Arg.Any<int>()).Returns(_ =>
        {
            if (_created is not null)
            {
                _created.Area = TestData.Area(AreaId, "Gulberg");
            }

            return _created;
        });

        _handler = new CreatePropertyCommandHandler(
            _properties, _locations, _facilities, _users, _unitOfWork, TestMapper.Create());
    }

    private static CreatePropertyCommand Command() => new()
    {
        OwnerId = OwnerId,
        AreaId = AreaId,
        Title = "  Bright shared room  ",
        Description = "A four-bed shared room in a well-kept portion.",
        AddressLine = "House 42, Block C, Gulberg III",
        Latitude = 31.5204,
        Longitude = 74.3587,
        RoomType = "Shared",
        GenderPolicy = "Boys",
        TotalBeds = 4,
        AvailableBeds = 2,
        MonthlyRent = 18000m
    };

    private Task<Application.DTOs.OwnerPropertyDto> HandleAsync(CreatePropertyCommand? command = null)
    {
        return _handler.Handle(command ?? Command(), CancellationToken.None);
    }

    [Fact]
    public async Task Rule_14_a_new_listing_is_a_draft()
    {
        await HandleAsync();

        _created.ShouldNotBeNull();
        _created!.Status.ShouldBe(PropertyStatus.Draft);
    }

    [Fact]
    public async Task The_listing_belongs_to_the_signed_in_owner()
    {
        await HandleAsync();

        _created!.OwnerId.ShouldBe(OwnerId);
    }

    [Fact]
    public async Task Availability_is_confirmed_as_of_creation()
    {
        await HandleAsync();

        _created!.AvailabilityConfirmedAt.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_pin_the_owner_dropped_is_what_is_stored()
    {
        await HandleAsync();

        _created!.Latitude.ShouldBe(31.5204);
        _created.Longitude.ShouldBe(74.3587);
    }

    [Fact]
    public async Task Surrounding_whitespace_is_trimmed_off_the_title()
    {
        await HandleAsync();

        _created!.Title.ShouldBe("Bright shared room");
    }

    [Fact]
    public async Task Charges_that_were_left_out_are_stored_as_zero()
    {
        await HandleAsync();

        _created!.SecurityDeposit.ShouldBe(0m);
        _created.UtilitiesCharge.ShouldBe(0m);
        _created.MessCharge.ShouldBe(0m);
    }

    [Fact]
    public async Task Blank_house_rules_are_stored_as_nothing_rather_than_an_empty_string()
    {
        var command = Command();
        command.HouseRules = "   ";

        await HandleAsync(command);

        _created!.HouseRules.ShouldBeNull();
    }

    [Fact]
    public async Task Rule_13_a_seeker_may_not_create_a_listing()
    {
        var seeker = TestData.User(OwnerId, "seeker@roomraah.local");
        seeker.SeekerProfile = new SeekerProfile();
        _users.GetByIdAsync(OwnerId).Returns(seeker);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync());
    }

    [Fact]
    public async Task Rule_13_an_account_that_no_longer_exists_may_not_create_a_listing()
    {
        _users.GetByIdAsync(OwnerId).Returns((AppUser?)null);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync());
    }

    [Fact]
    public async Task Rule_13_is_checked_before_anything_is_written()
    {
        _users.GetByIdAsync(OwnerId).Returns((AppUser?)null);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync());

        await _properties.DidNotReceive().CreateAsync(Arg.Any<Property>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_53_an_area_that_does_not_exist_is_refused()
    {
        _locations.AreaExistsAsync(AreaId).Returns(false);

        var error = await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync());

        error.Message.ShouldContain("area");
        await _properties.DidNotReceive().CreateAsync(Arg.Any<Property>());
    }

    [Fact]
    public async Task Rule_53_a_facility_that_does_not_exist_is_refused()
    {
        var command = Command();
        command.FacilityIds = [1, 2];
        _facilities.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Facility(1)]);

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(command));

        await _properties.DidNotReceive().CreateAsync(Arg.Any<Property>());
    }

    [Fact]
    public async Task The_facilities_that_were_chosen_are_attached()
    {
        var command = Command();
        command.FacilityIds = [1, 4];
        _facilities.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Facility(1), TestData.Facility(4, "Laundry", "laundry", 4)]);

        await HandleAsync(command);

        _created!.Facilities.Select(f => f.FacilityId).ShouldBe([1, 4]);
    }

    [Fact]
    public async Task The_same_facility_named_twice_is_attached_once()
    {
        var command = Command();
        command.FacilityIds = [1, 1];
        _facilities.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Facility(1)]);

        await HandleAsync(command);

        _created!.Facilities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_listing_with_no_facilities_does_not_ask_the_repository_for_any()
    {
        await HandleAsync();

        await _facilities.DidNotReceive().GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>());
        _created!.Facilities.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_listing_is_saved_once()
    {
        await HandleAsync();

        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task The_response_carries_the_area_and_city_the_listing_sits_in()
    {
        var result = await HandleAsync();

        result.AreaName.ShouldBe("Gulberg");
        result.CityName.ShouldBe("Lahore");
        result.Status.ShouldBe("Draft");
    }
}
