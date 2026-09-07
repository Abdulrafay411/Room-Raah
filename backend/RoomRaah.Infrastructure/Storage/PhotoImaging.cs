using SkiaSharp;

namespace RoomRaah.Infrastructure.Storage;

/// <summary>
/// Everything about a photograph that has nothing to do with where it is stored:
/// deciding whether the bytes are an image the product accepts, generating the
/// thumbnail, and making a browser's file name safe to put in a URL.
///
/// It lives here rather than inside a storage class because there are two storage
/// classes now. The same logic written twice is the same logic getting two different
/// bug fixes — and the part most worth getting right is the part that decides whether
/// an upload is an image at all.
/// </summary>
public static class PhotoImaging
{
    /// <summary>
    /// The three formats an upload may be in, and the content type each is stored
    /// with. Not configuration: adding a fourth means teaching the thumbnail encoder
    /// to read it, which no setting can do.
    /// </summary>
    public static readonly IReadOnlyDictionary<SKEncodedImageFormat, string> AcceptedFormats =
        new Dictionary<SKEncodedImageFormat, string>
        {
            [SKEncodedImageFormat.Jpeg] = "image/jpeg",
            [SKEncodedImageFormat.Png] = "image/png",
            [SKEncodedImageFormat.Webp] = "image/webp"
        };

    public const int ThumbnailQuality = 80;

    /// <summary>
    /// Reads the format out of the bytes rather than believing the Content-Type the
    /// client sent, which is exactly as easy to forge as a file extension.
    /// </summary>
    public static SKEncodedImageFormat? DetectFormat(byte[] bytes)
    {
        // Skia throws on nothing rather than answering "not an image", and the whole
        // contract of this method is that it answers. The upload validator refuses an
        // empty file long before this, so nothing reaches here today — but a guard
        // that lives two layers away from the function it protects is a guard that
        // stops protecting it the moment somebody calls the function from elsewhere.
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);

            if (codec is null || !AcceptedFormats.ContainsKey(codec.EncodedFormat))
            {
                return null;
            }

            return codec.EncodedFormat;
        }
        catch (Exception)
        {
            // Skia throws for some unreadable inputs instead of returning a null
            // codec. Either way the answer is the same: not a photograph.
            return null;
        }
    }

    /// <summary>
    /// Roughly <paramref name="longEdgePx"/> on the long edge, always JPEG.
    ///
    /// The thumbnail is not a nicety: a search page shows a dozen cards, and a dozen
    /// full-size photographs is a page that never finishes loading on mobile data.
    /// Encoding as JPEG rather than mirroring the original keeps one extension in the
    /// path and one decoder on the card — nothing in a photograph of a room needs
    /// transparency.
    /// </summary>
    public static MemoryStream? CreateThumbnail(byte[] bytes, int longEdgePx)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        // Skia throws rather than answering when it cannot read the bytes, and both
        // "empty" and "not an image at all" go down that path. This method's contract
        // is that it returns null, so the throw is turned back into the answer.
        //
        // In the product this is unreachable: DetectFormat has already accepted the
        // bytes before anything gets here. It is caught anyway, because a helper that
        // only keeps its contract when called in one particular order is a helper that
        // breaks the first time somebody calls it in another.
        SKBitmap? original;

        try
        {
            original = SKBitmap.Decode(bytes);
        }
        catch (Exception)
        {
            return null;
        }

        if (original is null)
        {
            return null;
        }

        using (original)
        {

            var longEdge = Math.Max(original.Width, original.Height);
            var scale = longEdge <= longEdgePx ? 1d : (double)longEdgePx / longEdge;

            var width = Math.Max(1, (int)Math.Round(original.Width * scale));
            var height = Math.Max(1, (int)Math.Round(original.Height * scale));

            using var resized = original.Resize(
                new SKImageInfo(width, height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

            if (resized is null)
            {
                return null;
            }

            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, ThumbnailQuality);

            var output = new MemoryStream();
            encoded.SaveTo(output);
            output.Position = 0;

            return output;
        }
    }

    /// <summary>
    /// A file name arrives from a browser and ends up inside a URL. Directory
    /// separators and anything unusual are replaced rather than trusted.
    /// </summary>
    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);

        if (string.IsNullOrWhiteSpace(name))
        {
            return "photo";
        }

        var cleaned = new string(name
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')
            .ToArray());

        return cleaned.Length <= 100 ? cleaned : cleaned[^100..];
    }

    /// <summary>
    /// The two object names one upload produces. Identical across providers on
    /// purpose: a deployment that moves from one to the other keeps its paths, and
    /// §9's documented layout is the same wherever the bytes end up.
    /// </summary>
    public static (string Original, string Thumbnail) NamesFor(
        int propertyId, string id, string fileName) =>
        ($"{propertyId}/{id}_{SafeFileName(fileName)}", $"{propertyId}/thumb_{id}.jpg");
}
