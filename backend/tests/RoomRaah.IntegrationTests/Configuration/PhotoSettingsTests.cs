using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Listings;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Configuration;

/// <summary>
/// The handler tests are given a PhotoSettings built in the test project, which
/// says nothing about what the running application is configured with. These read
/// the real configuration out of the booted host and pin it to the documented
/// values, so a limit changed in appsettings and nowhere else fails here.
/// </summary>
[Collection(ApiCollection.Name)]
public class PhotoSettingsTests
{
    private readonly PhotoSettings _settings;

    public PhotoSettingsTests(RoomRaahApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        _settings = scope.ServiceProvider.GetRequiredService<IOptions<PhotoSettings>>().Value;
    }

    [Fact]
    public void A_photograph_may_be_five_megabytes()
    {
        _settings.MaxFileSizeBytes.ShouldBe(5 * 1024 * 1024);
    }

    [Fact]
    public void A_listing_holds_between_three_and_fifteen_photographs()
    {
        _settings.MinPhotosPerListing.ShouldBe(3);
        _settings.MaxPhotosPerListing.ShouldBe(15);
    }

    [Fact]
    public void A_thumbnail_is_four_hundred_pixels_on_its_long_edge()
    {
        _settings.ThumbnailLongEdgePx.ShouldBe(400);
    }

    [Fact]
    public void Photographs_live_in_the_properties_container()
    {
        _settings.ContainerName.ShouldBe("properties");
    }

    [Fact]
    public void Three_formats_are_accepted_and_no_others()
    {
        PhotoSettings.AcceptedContentTypes.ShouldBe(["image/jpeg", "image/png", "image/webp"]);
    }
}
