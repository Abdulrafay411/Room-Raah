using RoomRaah.Infrastructure.Storage;
using RoomRaah.UnitTests.Common;
using Shouldly;
using SkiaSharp;

namespace RoomRaah.UnitTests.Storage;

/// <summary>
/// The half of photo handling that has nothing to do with where the bytes go.
///
/// It was inside the Azure implementation until there were two implementations. The
/// same logic written twice is the same logic getting two different bug fixes, and the
/// part most worth getting right is the part that decides whether an upload is an image
/// at all — the only thing standing between a listing gallery and an arbitrary file.
/// </summary>
public class PhotoImagingTests
{
    private static byte[] Jpeg(int width = 1200, int height = 800) =>
        Encode(SKEncodedImageFormat.Jpeg, width, height);

    private static byte[] Encode(SKEncodedImageFormat format, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0x12, 0x3B, 0x5D));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);

        return data.ToArray();
    }

    // ---------------------------------------------------------------- format

    [Fact]
    public void The_three_accepted_formats_are_recognised()
    {
        PhotoImaging.DetectFormat(Encode(SKEncodedImageFormat.Jpeg, 40, 40))
            .ShouldBe(SKEncodedImageFormat.Jpeg);

        PhotoImaging.DetectFormat(Encode(SKEncodedImageFormat.Png, 40, 40))
            .ShouldBe(SKEncodedImageFormat.Png);

        PhotoImaging.DetectFormat(Encode(SKEncodedImageFormat.Webp, 40, 40))
            .ShouldBe(SKEncodedImageFormat.Webp);
    }

    /// <summary>
    /// §9: the format is read out of the bytes, not out of the Content-Type the client
    /// sent — which is exactly as easy to forge as a file extension.
    /// </summary>
    [Fact]
    public void Something_that_is_not_an_image_is_refused_however_it_is_named()
    {
        PhotoImaging.DetectFormat("this is a text file, not a photograph"u8.ToArray())
            .ShouldBeNull();
    }

    [Fact]
    public void An_empty_upload_is_refused()
    {
        PhotoImaging.DetectFormat([]).ShouldBeNull();
    }

    /// <summary>A format Skia can read but the product does not accept is still refused.</summary>
    [Fact]
    public void A_gif_is_not_one_of_the_three()
    {
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

        PhotoImaging.DetectFormat(gif).ShouldBeNull();
    }

    // ------------------------------------------------------------- thumbnail

    [Fact]
    public void A_thumbnail_is_scaled_to_the_long_edge_and_keeps_its_shape()
    {
        using var thumbnail = PhotoImaging.CreateThumbnail(Jpeg(1200, 800), 400);

        thumbnail.ShouldNotBeNull();

        using var decoded = SKBitmap.Decode(thumbnail!.ToArray());

        decoded.Width.ShouldBe(400);
        decoded.Height.ShouldBe(267);
    }

    [Fact]
    public void A_tall_photograph_is_scaled_by_its_height()
    {
        using var thumbnail = PhotoImaging.CreateThumbnail(Jpeg(600, 1200), 400);

        using var decoded = SKBitmap.Decode(thumbnail!.ToArray());

        decoded.Height.ShouldBe(400);
        decoded.Width.ShouldBe(200);
    }

    /// <summary>
    /// A photograph already smaller than the thumbnail is left alone. Scaling it up
    /// would make a bigger file that looks worse.
    /// </summary>
    [Fact]
    public void A_photograph_smaller_than_the_thumbnail_is_not_enlarged()
    {
        using var thumbnail = PhotoImaging.CreateThumbnail(Jpeg(120, 90), 400);

        using var decoded = SKBitmap.Decode(thumbnail!.ToArray());

        decoded.Width.ShouldBe(120);
        decoded.Height.ShouldBe(90);
    }

    /// <summary>Always JPEG, whatever went in — one extension in the path, one decoder on the card.</summary>
    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void A_thumbnail_is_always_a_jpeg(SKEncodedImageFormat source)
    {
        using var thumbnail = PhotoImaging.CreateThumbnail(Encode(source, 800, 600), 400);

        PhotoImaging.DetectFormat(thumbnail!.ToArray()).ShouldBe(SKEncodedImageFormat.Jpeg);
    }

    [Fact]
    public void Bytes_that_are_not_an_image_produce_no_thumbnail()
    {
        PhotoImaging.CreateThumbnail("not an image"u8.ToArray(), 400).ShouldBeNull();
    }

    // ------------------------------------------------------------- file name

    /// <summary>
    /// A file name arrives from a browser and ends up inside a URL. This is the only
    /// thing standing between that and a key containing a path.
    /// </summary>
    [Theory]
    [InlineData("room.jpg", "room.jpg")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\me\\room.jpg", "room.jpg")]
    [InlineData("front room (1).jpg", "front_room__1_.jpg")]
    [InlineData("../secret", "secret")]
    public void A_file_name_is_reduced_to_something_safe(string given, string expected)
    {
        PhotoImaging.SafeFileName(given).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void A_file_name_with_nothing_in_it_becomes_a_placeholder(string given)
    {
        PhotoImaging.SafeFileName(given).ShouldBe("photo");
    }

    [Fact]
    public void A_very_long_file_name_is_cut_to_a_hundred_characters()
    {
        PhotoImaging.SafeFileName(new string('a', 400) + ".jpg").Length.ShouldBe(100);
    }

    // ------------------------------------------------------------------ keys

    /// <summary>
    /// §9's documented layout, and identical across providers on purpose: a deployment
    /// that moves from Azure to S3 keeps every path it has already written.
    /// </summary>
    [Fact]
    public void The_two_object_names_follow_the_documented_layout()
    {
        var (original, thumbnail) = PhotoImaging.NamesFor(42, "abc123", "front room.jpg");

        original.ShouldBe("42/abc123_front_room.jpg");
        thumbnail.ShouldBe("42/thumb_abc123.jpg");
    }

    [Fact]
    public void An_unsafe_file_name_cannot_escape_its_listing_folder()
    {
        var (original, _) = PhotoImaging.NamesFor(42, "abc123", "../../7/evil.jpg");

        original.ShouldBe("42/abc123_evil.jpg");
        original.ShouldNotContain("..");
    }
}
