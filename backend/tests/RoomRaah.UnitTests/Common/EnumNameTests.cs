using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;
using Shouldly;

namespace RoomRaah.UnitTests.Common;

/// <summary>
/// The gap between "parses" and "names something". Every enumeration a client is
/// allowed to send travels as a string, and until this helper existed a request
/// naming a number outside the enumeration was accepted and stored.
/// </summary>
public class EnumNameTests
{
    [Theory]
    [InlineData("Physical")]
    [InlineData("video")]
    [InlineData("  Physical  ")]
    public void A_name_the_enumeration_has_is_accepted(string value)
    {
        EnumName.IsDefined<VisitType>(value).ShouldBeTrue();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("99")]
    public void A_number_outside_the_enumeration_is_refused(string value)
    {
        EnumName.IsDefined<VisitType>(value).ShouldBeFalse();
        EnumName.Parse<VisitType>(value).ShouldBeNull();
    }

    /// <summary>
    /// A number that happens to land on a value is still not a name anybody wrote,
    /// but it is a value the product has, so it is allowed through rather than
    /// refused on a technicality the client cannot see.
    /// </summary>
    [Fact]
    public void A_number_that_lands_on_a_value_is_that_value()
    {
        EnumName.Parse<VisitType>("1").ShouldBe(VisitType.Video);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Inspection")]
    public void Nothing_and_nonsense_are_refused(string? value)
    {
        EnumName.IsDefined<VisitType>(value).ShouldBeFalse();
    }

    [Fact]
    public void A_refusal_leaves_the_out_parameter_at_its_default()
    {
        EnumName.TryParse<ReportStatus>("77", out var parsed).ShouldBeFalse();
        parsed.ShouldBe(default(ReportStatus));
    }
}
