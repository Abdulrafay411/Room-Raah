using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Services;
using Shouldly;

namespace RoomRaah.UnitTests.Search;

/// <summary>
/// Rule 31. These run the expression in memory, which is not proof that SQL Server
/// translates it — the integration suite is where that is settled. What they prove is
/// that the formula is the right one, and that the filter and the number agree.
/// </summary>
public class HaversineDistanceCalculatorTests
{
    private readonly HaversineDistanceCalculator _calculator = new();

    private static Property At(double latitude, double longitude) =>
        new() { Latitude = latitude, Longitude = longitude };

    private double Kilometres(double fromLat, double fromLng, Property property) =>
        _calculator.KilometresFrom(fromLat, fromLng).Compile()(property);

    [Fact]
    public void A_listing_at_the_landmark_is_no_distance_away()
    {
        Kilometres(31.5204, 74.3587, At(31.5204, 74.3587)).ShouldBe(0, 0.0001);
    }

    /// <summary>
    /// One degree of latitude is the same length everywhere on a sphere, which makes
    /// it the one distance that can be checked without trusting a second calculator.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(-45)]
    public void One_degree_of_latitude_is_a_hundred_and_eleven_kilometres(double startingLatitude)
    {
        Kilometres(startingLatitude, 0, At(startingLatitude + 1, 0)).ShouldBe(111.1951, 0.001);
    }

    [Theory]
    [InlineData(31.5204, 74.3587, 33.6844, 73.0479, 270.1363)]  // Lahore to Islamabad
    [InlineData(31.4966, 74.2996, 31.5204, 74.3587, 6.1963)]    // Punjab University to Gulberg
    [InlineData(33.6425, 72.9906, 33.6650, 73.0750, 8.2028)]    // NUST to I-8
    [InlineData(33.6425, 72.9906, 33.6680, 72.9930, 2.8442)]    // NUST to G-11
    [InlineData(31.5204, 74.3587, 24.8607, 67.0011, 1033.0003)] // Lahore to Karachi
    public void Real_distances_come_out_right(
        double fromLat, double fromLng, double toLat, double toLng, double expected)
    {
        Kilometres(fromLat, fromLng, At(toLat, toLng)).ShouldBe(expected, 0.01);
    }

    /// <summary>
    /// Short distances are where the spherical law of cosines loses its precision,
    /// and two hostels on the same road is the case this product deals in most.
    /// </summary>
    [Fact]
    public void Two_listings_a_few_hundred_metres_apart_are_not_the_same_distance()
    {
        var near = Kilometres(31.5204, 74.3587, At(31.5214, 74.3587));
        var nearer = Kilometres(31.5204, 74.3587, At(31.5209, 74.3587));

        near.ShouldBeGreaterThan(nearer);
        near.ShouldBe(0.1112, 0.001);
    }

    [Fact]
    public void Measuring_the_other_way_round_gives_the_same_answer()
    {
        var there = Kilometres(31.5204, 74.3587, At(33.6844, 73.0479));
        var back = Kilometres(33.6844, 73.0479, At(31.5204, 74.3587));

        there.ShouldBe(back, 0.0001);
    }

    /// <summary>
    /// The filter and the sort have to be the same formula. A filter that disagreed
    /// by a rounding error would drop a listing out of a page for no reason a reader
    /// could see, and would do it only near the boundary.
    /// </summary>
    [Theory]
    [InlineData(2.0)]
    [InlineData(6.1963)]
    [InlineData(6.2)]
    [InlineData(50)]
    public void The_filter_agrees_with_the_measurement(double limit)
    {
        var listing = At(31.5204, 74.3587);

        var measured = Kilometres(31.4966, 74.2996, listing);
        var within = _calculator.Within(31.4966, 74.2996, limit).Compile()(listing);

        within.ShouldBe(measured <= limit);
    }

    [Fact]
    public void A_listing_exactly_on_the_limit_is_within_it()
    {
        var listing = At(31.5204, 74.3587);
        var measured = Kilometres(31.4966, 74.2996, listing);

        _calculator.Within(31.4966, 74.2996, measured).Compile()(listing).ShouldBeTrue();
    }

    /// <summary>
    /// The expression has to stand on its own to reach SQL Server: a body that
    /// captured the calculator, or called back into it, would be pulled into memory
    /// with the whole table behind it.
    /// </summary>
    [Fact]
    public void The_expression_takes_one_listing_and_returns_a_number()
    {
        var expression = _calculator.KilometresFrom(31.5204, 74.3587);

        expression.Parameters.Count.ShouldBe(1);
        expression.Parameters[0].Type.ShouldBe(typeof(Property));
        expression.ReturnType.ShouldBe(typeof(double));
    }
}
