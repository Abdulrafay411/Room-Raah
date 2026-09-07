using System.Net.Http.Headers;
using SkiaSharp;

namespace RoomRaah.IntegrationTests.Infrastructure;

/// <summary>
/// Real image bytes, drawn here rather than checked into the repository. The upload
/// path decodes what it is given, so a test that posts a fabricated byte array is
/// testing the refusal path whether it means to or not.
/// </summary>
public static class TestImages
{
    public static byte[] Jpeg(int width = 1200, int height = 800) =>
        Encode(SKEncodedImageFormat.Jpeg, width, height);

    public static byte[] Png(int width = 600, int height = 600) =>
        Encode(SKEncodedImageFormat.Png, width, height);

    public static byte[] Webp(int width = 600, int height = 400) =>
        Encode(SKEncodedImageFormat.Webp, width, height);

    /// <summary>Bytes that will not decode, however they are labelled.</summary>
    public static byte[] NotAnImage() => "this is a text file wearing a photograph's name"u8.ToArray();

    private static byte[] Encode(SKEncodedImageFormat format, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(new SKColor(0x2E, 0x86, 0xAB));

        using var paint = new SKPaint { Color = new SKColor(0xF6, 0xAE, 0x2D), IsAntialias = true };
        surface.Canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 4f, paint);

        using var image = surface.Snapshot();
        using var data = image.Encode(format, 90);

        return data.ToArray();
    }

    public static MultipartFormDataContent AsUpload(
        byte[] bytes, string fileName = "room.jpg", string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    /// <summary>Reads a stored image back and reports how big it actually is.</summary>
    public static async Task<(int Width, int Height, string? ContentType)> MeasureAsync(string url)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var bitmap = SKBitmap.Decode(bytes);

        return (bitmap.Width, bitmap.Height, response.Content.Headers.ContentType?.MediaType);
    }
}
