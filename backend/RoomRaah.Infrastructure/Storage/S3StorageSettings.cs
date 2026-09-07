namespace RoomRaah.Infrastructure.Storage;

/// <summary>
/// Where photographs live when the product is deployed on AWS.
///
/// This is deployment configuration, not product configuration, which is why it is
/// here and not beside <c>PhotoSettings</c> in the Application layer: how large a
/// thumbnail is and how many photographs a listing may hold are decisions the product
/// document makes, and which bucket in which region holds the bytes is not.
/// </summary>
public class S3StorageSettings
{
    /// <summary>The bucket. Required when the S3 provider is selected.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// The base the stored URLs are built from — a CloudFront domain, or a bucket
    /// website endpoint.
    ///
    /// Set it, and the URLs the product hands out stop naming the bucket, so the
    /// bytes can move behind a CDN or to another bucket without rewriting every row.
    /// Leave it unset and the SDK's own bucket URL is used, which is fine for a test
    /// deployment and a hostage to fortune for a real one.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// An explicit endpoint, for MinIO or LocalStack. Left unset in AWS, where the
    /// SDK resolves the real one from the region.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Path-style addressing — <c>host/bucket/key</c> rather than
    /// <c>bucket.host/key</c>. Required by MinIO and by anything else that cannot
    /// answer on a wildcard subdomain; unnecessary against AWS itself.
    /// </summary>
    public bool ForcePathStyle { get; set; }
}
