using FluentValidation.TestHelper;
using Shouldly;
using RoomRaah.Application.Features.Locations.Queries;

namespace RoomRaah.UnitTests.Locations;

public class GetAreasQueryValidatorTests
{
    private readonly GetAreasQueryValidator _validator = new();

    [Fact]
    public void Accepts_a_real_city_id()
    {
        _validator.TestValidate(new GetAreasQuery { CityId = 1 }).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_city_id_must_be_positive(int cityId)
    {
        _validator.TestValidate(new GetAreasQuery { CityId = cityId })
            .ShouldHaveValidationErrorFor(x => x.CityId);
    }
}

public class SuggestLocationsQueryValidatorTests
{
    private readonly SuggestLocationsQueryValidator _validator = new();

    private static SuggestLocationsQuery Query(string q) => new() { Q = q };

    [Theory]
    [InlineData("nu")]
    [InlineData("nust")]
    [InlineData("F-7")]
    public void Accepts_a_term_of_at_least_two_characters(string q)
    {
        _validator.TestValidate(Query(q)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("n")]
    [InlineData("  n  ")]
    public void A_term_shorter_than_two_characters_is_rejected(string q)
    {
        _validator.TestValidate(Query(q)).ShouldHaveValidationErrorFor("q");
    }

    [Fact]
    public void Padding_does_not_count_towards_the_minimum()
    {
        _validator.TestValidate(Query("  nu  ")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_term_longer_than_sixty_characters_is_rejected()
    {
        var tooLong = new string('a', SuggestLocationsQuery.MaxQueryLength + 1);

        _validator.TestValidate(Query(tooLong)).ShouldHaveValidationErrorFor("q");
    }

    [Fact]
    public void A_term_of_exactly_sixty_characters_is_accepted()
    {
        var atTheLimit = new string('a', SuggestLocationsQuery.MaxQueryLength);

        _validator.TestValidate(Query(atTheLimit)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void The_field_is_reported_as_it_appears_in_the_query_string()
    {
        var result = _validator.TestValidate(Query("n"));

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("q");
    }
}
