using System.Linq.Expressions;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Services;
using Shouldly;

namespace RoomRaah.UnitTests.Search;

/// <summary>
/// Rule 77, asserted against the expression tree rather than against a database.
/// Paging through real rows only fails when SQL Server happens to reorder the ties,
/// which it is free not to do — and a test that passes when it is free to fail is
/// not a test of this rule at all.
/// </summary>
public class ListingOrderTests
{
    private static readonly IQueryable<Property> Nothing =
        Array.Empty<Property>().AsQueryable();

    private static IOrderedQueryable<Property> Ordered(PropertySort sort)
    {
        var criteria = new PropertySearchCriteria
        {
            Sort = sort,
            LandmarkLatitude = 33.6425,
            LandmarkLongitude = 72.9906
        };

        return ListingOrder.Apply(Nothing, criteria, new HaversineDistanceCalculator());
    }

    private static LambdaExpression KeySelector(Expression argument)
    {
        return (LambdaExpression)(argument is UnaryExpression { NodeType: ExpressionType.Quote } quote
            ? quote.Operand
            : argument);
    }

    public static TheoryData<PropertySort> EverySort()
    {
        var data = new TheoryData<PropertySort>();

        foreach (var sort in Enum.GetValues<PropertySort>())
        {
            data.Add(sort);
        }

        return data;
    }

    /// <summary>
    /// Walks every value of the enumeration, so a sort added in a later phase is
    /// covered the moment it is written rather than when somebody remembers.
    /// </summary>
    [Theory]
    [MemberData(nameof(EverySort))]
    public void Rule_77_every_order_ends_with_the_listing_id(PropertySort sort)
    {
        var call = (MethodCallExpression)Ordered(sort).Expression;

        call.Method.Name.ShouldBeOneOf("ThenBy", "ThenByDescending",
            $"the {sort} order does not end with a tie-break at all");

        var member = KeySelector(call.Arguments[1]).Body as MemberExpression;

        member.ShouldNotBeNull($"the {sort} tie-break is not a property of the listing");
        member.Member.Name.ShouldBe(nameof(Property.Id),
            $"the {sort} order is broken by {member.Member.Name} rather than by the id");
    }

    [Theory]
    [MemberData(nameof(EverySort))]
    public void Every_sort_the_product_offers_produces_an_order(PropertySort sort)
    {
        Ordered(sort).ShouldNotBeNull();
    }

    [Fact]
    public void The_default_is_the_most_recently_confirmed_first()
    {
        var call = (MethodCallExpression)Ordered(PropertySort.Recent).Expression;
        var outer = (MethodCallExpression)call.Arguments[0];

        outer.Method.Name.ShouldBe("OrderByDescending");
        ((MemberExpression)KeySelector(outer.Arguments[1]).Body).Member.Name
            .ShouldBe(nameof(Property.AvailabilityConfirmedAt));
    }

    [Fact]
    public void Cheapest_first_sorts_on_the_rent()
    {
        var call = (MethodCallExpression)Ordered(PropertySort.PriceLowToHigh).Expression;
        var outer = (MethodCallExpression)call.Arguments[0];

        outer.Method.Name.ShouldBe("OrderBy");
        ((MemberExpression)KeySelector(outer.Arguments[1]).Body).Member.Name
            .ShouldBe(nameof(Property.MonthlyRent));
    }

    /// <summary>
    /// Rule 74 keeps this from happening, and the fallback keeps it from being an
    /// exception if it ever does.
    /// </summary>
    [Fact]
    public void Sorting_by_distance_with_no_landmark_falls_back_rather_than_throwing()
    {
        var criteria = new PropertySearchCriteria { Sort = PropertySort.Distance };

        var call = (MethodCallExpression)ListingOrder
            .Apply(Nothing, criteria, new HaversineDistanceCalculator()).Expression;

        var outer = (MethodCallExpression)call.Arguments[0];

        ((MemberExpression)KeySelector(outer.Arguments[1]).Body).Member.Name
            .ShouldBe(nameof(Property.AvailabilityConfirmedAt));
    }

    [Fact]
    public void Sorting_by_distance_measures_from_the_landmark()
    {
        var call = (MethodCallExpression)Ordered(PropertySort.Distance).Expression;
        var outer = (MethodCallExpression)call.Arguments[0];

        outer.Method.Name.ShouldBe("OrderBy");

        // Not a property of the listing: the whole haversine, which is what puts the
        // sort in the database rather than in memory.
        KeySelector(outer.Arguments[1]).Body.ShouldNotBeOfType<MemberExpression>();
    }
}
