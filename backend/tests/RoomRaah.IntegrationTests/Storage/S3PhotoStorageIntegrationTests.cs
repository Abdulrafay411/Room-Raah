using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Listings;
using RoomRaah.Infrastructure.Storage;
using Shouldly;
using SkiaSharp;
using Testcontainers.Minio;

namespace RoomRaah.IntegrationTests.Storage;

/// <summary>
/// The S3 implementation against real S3-compatible storage, in a container created for
/// this class and thrown away afterwards.
///
/// It has its own container rather than joining the shared API fixture, because it
/// tests one service and the rest of the suite has no use for a bucket. The reason it
/// exists at all is that the AWS deployment runs on this code, and the unit tests can
/// only prove what the class *asks* for — whether the bytes come back, and whether a
/// delete really deletes, is something only a real object store can answer.
///
/// MinIO rather than LocalStack: it speaks the same API, starts in about a second, and
/// path-style addressing is what a deployment behind a CDN ends up using anyway.
/// </summary>
public class S3PhotoStorageIntegrationTests : IAsyncLifetime
{
    private const string Bucket = "roomraah-photos";

    // Pinned, like every other container in this suite: a stand-in that changes
    // underneath the tests is a stand-in that fails for reasons nobody changed.
    private readonly MinioContainer _minio =
        new MinioBuilder("minio/minio:RELEASE.2025-04-22T22-12-26Z").Build();

    private IAmazonS3 _s3 = null!;
    private S3PhotoStorageService _storage = null!;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();

        var endpoint = $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";

        _s3 = new AmazonS3Client(
            new BasicAWSCredentials(_minio.GetAccessKey(), _minio.GetSecretKey()),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",

                // Version 4 of the SDK sends a trailing checksum on every upload by
                // default, and MinIO answers that it does not match what it computed.
                // This is the stand-in disagreeing with the SDK, not the product
                // disagreeing with S3 — real S3 accepts the default, and the product's
                // own client is left on it. Turned off here so the test exercises the
                // storage service rather than an incompatibility between two things
                // that are not the storage service.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });

        await _s3.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });

        _storage = new S3PhotoStorageService(
            _s3,
            Options.Create(new PhotoSettings { ThumbnailLongEdgePx = 400 }),
            Options.Create(new S3StorageSettings
            {
                BucketName = Bucket,
                ServiceUrl = endpoint,
                ForcePathStyle = true
            }));
    }

    public async Task DisposeAsync()
    {
        _s3?.Dispose();
        await _minio.DisposeAsync();
    }

    private static Stream Jpeg(int width = 1200, int height = 800)
    {
        using var bitmap = new SKBitmap(width, height);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0x12, 0x3B, 0x5D));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

        return new MemoryStream(data.ToArray());
    }

    private async Task<byte[]?> FetchAsync(string key)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(Bucket, key);
            using var buffer = new MemoryStream();

            await response.ResponseStream.CopyToAsync(buffer);

            return buffer.ToArray();
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string KeyOf(string url) =>
        url[(url.IndexOf($"/{Bucket}/", StringComparison.Ordinal) + Bucket.Length + 2)..];

    [Fact]
    public async Task An_upload_really_stores_bytes_that_come_back_as_an_image()
    {
        var stored = await _storage.UploadAsync(42, Jpeg(), "front room.jpg");

        stored.ShouldNotBeNull();

        var original = await FetchAsync(KeyOf(stored!.BlobUrl));
        var thumbnail = await FetchAsync(KeyOf(stored.ThumbnailUrl));

        original.ShouldNotBeNull();
        thumbnail.ShouldNotBeNull();

        // Not just "something arrived" — something that decodes as the picture it was.
        using var decodedOriginal = SKBitmap.Decode(original);
        using var decodedThumbnail = SKBitmap.Decode(thumbnail);

        decodedOriginal.Width.ShouldBe(1200);
        decodedThumbnail.Width.ShouldBe(400);
    }

    /// <summary>
    /// The whole reason a thumbnail exists: a search page shows a dozen cards, and a
    /// dozen full-size photographs is a page that never finishes loading on mobile data.
    /// A thumbnail that is not much smaller is not doing its job.
    /// </summary>
    [Fact]
    public async Task The_thumbnail_is_a_great_deal_smaller_than_the_original()
    {
        var stored = await _storage.UploadAsync(42, Jpeg(2400, 1600), "big.jpg");

        var original = await FetchAsync(KeyOf(stored!.BlobUrl));
        var thumbnail = await FetchAsync(KeyOf(stored.ThumbnailUrl));

        thumbnail!.Length.ShouldBeLessThan(original!.Length / 2);
    }

    [Fact]
    public async Task The_stored_content_types_are_the_ones_a_browser_needs()
    {
        var stored = await _storage.UploadAsync(42, Jpeg(), "room.jpg");

        var original = await _s3.GetObjectMetadataAsync(Bucket, KeyOf(stored!.BlobUrl));
        var thumbnail = await _s3.GetObjectMetadataAsync(Bucket, KeyOf(stored.ThumbnailUrl));

        original.Headers.ContentType.ShouldBe("image/jpeg");
        thumbnail.Headers.ContentType.ShouldBe("image/jpeg");
    }

    /// <summary>
    /// The one the unit tests cannot answer. A delete that resolves the key correctly
    /// but never reaches the store leaves the file behind for ever, and nothing on a
    /// screen would ever show it.
    /// </summary>
    [Fact]
    public async Task A_delete_really_removes_both_objects()
    {
        var stored = await _storage.UploadAsync(42, Jpeg(), "room.jpg");

        var originalKey = KeyOf(stored!.BlobUrl);
        var thumbnailKey = KeyOf(stored.ThumbnailUrl);

        (await FetchAsync(originalKey)).ShouldNotBeNull();

        await _storage.DeleteAsync(stored.BlobUrl, stored.ThumbnailUrl);

        (await FetchAsync(originalKey)).ShouldBeNull();
        (await FetchAsync(thumbnailKey)).ShouldBeNull();
    }

    /// <summary>An object that is already gone is not an error — the row went first, on purpose.</summary>
    [Fact]
    public async Task Deleting_something_that_is_not_there_is_not_a_failure()
    {
        var stored = await _storage.UploadAsync(42, Jpeg(), "room.jpg");

        await _storage.DeleteAsync(stored!.BlobUrl, stored.ThumbnailUrl);

        await Should.NotThrowAsync(() => _storage.DeleteAsync(stored.BlobUrl, stored.ThumbnailUrl));
    }

    /// <summary>
    /// Two uploads of the same file name must not collide. The random id in the key is
    /// what keeps them apart, and without it the second upload would silently replace
    /// the first — a listing losing a photograph it still shows a row for.
    /// </summary>
    [Fact]
    public async Task Two_uploads_of_the_same_file_name_are_two_objects()
    {
        var first = await _storage.UploadAsync(42, Jpeg(), "room.jpg");
        var second = await _storage.UploadAsync(42, Jpeg(), "room.jpg");

        first!.BlobUrl.ShouldNotBe(second!.BlobUrl);

        (await FetchAsync(KeyOf(first.BlobUrl))).ShouldNotBeNull();
        (await FetchAsync(KeyOf(second.BlobUrl))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Something_that_is_not_an_image_reaches_the_bucket_at_all()
    {
        var stored = await _storage.UploadAsync(
            42, new MemoryStream("this is a text file"u8.ToArray()), "room.jpg");

        stored.ShouldBeNull();

        var everything = await _s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = Bucket,
            Prefix = "42/"
        });

        // Nothing this test wrote, and nothing left half-written either. The list
        // comes back null rather than empty when the prefix holds nothing, which is
        // the answer this test wants and the shape it does not.
        (everything.S3Objects ?? []).ShouldAllBe(o => !o.Key.EndsWith("_room.jpg"));
    }
}
