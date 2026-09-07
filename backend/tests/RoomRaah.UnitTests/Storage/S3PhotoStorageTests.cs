using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using NSubstitute;
using RoomRaah.Application.Listings;
using RoomRaah.Infrastructure.Storage;
using Shouldly;
using SkiaSharp;

namespace RoomRaah.UnitTests.Storage;

/// <summary>
/// The part of the S3 implementation that is arithmetic rather than networking: which
/// key an upload writes, which URL it hands back, and — the one that actually matters —
/// whether a delete can find the object again from the URL it stored.
///
/// A delete that silently matches nothing leaves the file behind for ever and costs
/// money quietly, which is exactly the kind of fault that never shows up on a screen.
/// </summary>
public class S3PhotoStorageTests
{
    private const string Bucket = "roomraah-photos";

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();

    private readonly List<PutObjectRequest> _puts = [];
    private readonly List<DeleteObjectRequest> _deletes = [];

    public S3PhotoStorageTests()
    {
        _s3.Config.Returns(new AmazonS3Config { RegionEndpoint = RegionEndpoint.APSouth1 });

        _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(_puts.Add), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        _s3.DeleteObjectAsync(Arg.Do<DeleteObjectRequest>(_deletes.Add), Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());
    }

    private S3PhotoStorageService Service(S3StorageSettings? storage = null)
    {
        return new S3PhotoStorageService(
            _s3,
            Options.Create(new PhotoSettings { ThumbnailLongEdgePx = 400 }),
            Options.Create(storage ?? new S3StorageSettings { BucketName = Bucket }));
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

    // ---------------------------------------------------------------- upload

    [Fact]
    public async Task An_upload_writes_the_original_and_a_thumbnail()
    {
        var stored = await Service().UploadAsync(42, Jpeg(), "front room.jpg");

        stored.ShouldNotBeNull();
        _puts.Count.ShouldBe(2);

        _puts.ShouldAllBe(p => p.BucketName == Bucket);
        _puts[0].ContentType.ShouldBe("image/jpeg");
        _puts[1].Key.ShouldStartWith("42/thumb_");
        _puts[1].ContentType.ShouldBe("image/jpeg");
    }

    /// <summary>§9's layout, and the same keys the Azure implementation writes.</summary>
    [Fact]
    public async Task The_keys_follow_the_documented_layout()
    {
        await Service().UploadAsync(7, Jpeg(), "front room.jpg");

        _puts[0].Key.ShouldMatch(@"^7/[0-9a-f]{32}_front_room\.jpg$");
        _puts[1].Key.ShouldMatch(@"^7/thumb_[0-9a-f]{32}\.jpg$");
    }

    [Fact]
    public async Task A_png_is_stored_as_a_png_and_thumbnailed_as_a_jpeg()
    {
        using var bitmap = new SKBitmap(600, 400);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);

        await Service().UploadAsync(1, new MemoryStream(data.ToArray()), "room.png");

        _puts[0].ContentType.ShouldBe("image/png");
        _puts[1].ContentType.ShouldBe("image/jpeg");
    }

    /// <summary>
    /// The contract the handler depends on: null means "these bytes are not a
    /// photograph", and nothing was written.
    /// </summary>
    [Fact]
    public async Task Something_that_is_not_an_image_stores_nothing()
    {
        var stored = await Service().UploadAsync(
            1, new MemoryStream("this is a text file"u8.ToArray()), "room.jpg");

        stored.ShouldBeNull();
        _puts.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------- URL

    [Fact]
    public async Task Without_a_configured_base_the_url_names_the_bucket_and_the_region()
    {
        var stored = await Service().UploadAsync(42, Jpeg(), "room.jpg");

        stored!.BlobUrl.ShouldStartWith($"https://s3.ap-south-1.amazonaws.com/{Bucket}/42/");
    }

    [Fact]
    public async Task An_explicit_endpoint_is_used_when_one_is_set()
    {
        var stored = await Service(new S3StorageSettings
        {
            BucketName = Bucket,
            ServiceUrl = "http://localhost:9000",
            ForcePathStyle = true
        }).UploadAsync(42, Jpeg(), "room.jpg");

        stored!.BlobUrl.ShouldStartWith($"http://localhost:9000/{Bucket}/42/");
    }

    /// <summary>
    /// A configured base is what lets the bytes move behind a CDN later without
    /// rewriting a single stored row, so it has to win over everything else.
    /// </summary>
    [Fact]
    public async Task A_configured_public_base_wins_and_hides_the_bucket()
    {
        var stored = await Service(new S3StorageSettings
        {
            BucketName = Bucket,
            PublicBaseUrl = "https://photos.roomraah.pk/"
        }).UploadAsync(42, Jpeg(), "room.jpg");

        stored!.BlobUrl.ShouldStartWith("https://photos.roomraah.pk/42/");
        stored.BlobUrl.ShouldNotContain(Bucket);
    }

    // ---------------------------------------------------------------- delete

    /// <summary>
    /// The round trip. Whatever URL an upload handed out, a delete has to turn it back
    /// into the key it came from — otherwise the row goes and the file stays for ever.
    /// </summary>
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("http://localhost:9000", null, true)]
    [InlineData(null, "https://photos.roomraah.pk", false)]
    [InlineData("http://localhost:9000", "https://photos.roomraah.pk/", true)]
    public async Task A_delete_finds_the_key_behind_the_url_it_stored(
        string? serviceUrl, string? publicBase, bool pathStyle)
    {
        var settings = new S3StorageSettings
        {
            BucketName = Bucket,
            ServiceUrl = serviceUrl,
            PublicBaseUrl = publicBase,
            ForcePathStyle = pathStyle
        };

        var service = Service(settings);

        var stored = await service.UploadAsync(42, Jpeg(), "front room.jpg");

        var writtenKeys = _puts.Select(p => p.Key).ToList();

        await service.DeleteAsync(stored!.BlobUrl, stored.ThumbnailUrl);

        _deletes.Select(d => d.Key).ShouldBe(writtenKeys);
        _deletes.ShouldAllBe(d => d.BucketName == Bucket);
    }

    /// <summary>
    /// A row written before <c>PublicBaseUrl</c> was turned on still has to be
    /// deletable, or turning the setting on strands every file already stored.
    /// </summary>
    [Fact]
    public async Task A_url_written_before_the_public_base_was_configured_still_deletes()
    {
        var settings = new S3StorageSettings
        {
            BucketName = Bucket,
            PublicBaseUrl = "https://photos.roomraah.pk"
        };

        await Service(settings).DeleteAsync(
            $"https://s3.ap-south-1.amazonaws.com/{Bucket}/42/abc_room.jpg",
            $"https://s3.ap-south-1.amazonaws.com/{Bucket}/42/thumb_abc.jpg");

        _deletes.Select(d => d.Key).ShouldBe(["42/abc_room.jpg", "42/thumb_abc.jpg"]);
    }

    /// <summary>Virtual-host addressing, which is what AWS uses by default.</summary>
    [Fact]
    public async Task A_virtual_host_style_url_is_understood_too()
    {
        await Service().DeleteAsync(
            $"https://{Bucket}.s3.ap-south-1.amazonaws.com/42/abc_room.jpg",
            $"https://{Bucket}.s3.ap-south-1.amazonaws.com/42/thumb_abc.jpg");

        _deletes.Select(d => d.Key).ShouldBe(["42/abc_room.jpg", "42/thumb_abc.jpg"]);
    }

    [Fact]
    public async Task A_url_that_belongs_to_nothing_is_ignored_rather_than_guessed_at()
    {
        await Service().DeleteAsync("https://example.com/somebody-elses/file.jpg", "not a url");

        _deletes.ShouldBeEmpty();
    }

    /// <summary>
    /// The same decision the Azure implementation makes: the row is already gone, so a
    /// storage failure must not fail the request. A file nobody can reach is a bill,
    /// not a broken page.
    /// </summary>
    [Fact]
    public async Task A_storage_failure_during_delete_does_not_reach_the_caller()
    {
        _s3.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<DeleteObjectResponse>>(_ => throw new AmazonS3Exception("gone"));

        await Should.NotThrowAsync(() => Service().DeleteAsync(
            $"https://s3.ap-south-1.amazonaws.com/{Bucket}/42/abc_room.jpg",
            $"https://s3.ap-south-1.amazonaws.com/{Bucket}/42/thumb_abc.jpg"));
    }
}
