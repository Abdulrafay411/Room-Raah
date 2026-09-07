namespace RoomRaah.Application.Caching;

/// <summary>
/// Every cache key in the product is built here rather than typed at a call site,
/// so that the handler that writes a key and the code that evicts it can never
/// drift apart.
/// </summary>
public static class CacheKeys
{
    public const string LocationsPrefix = "locations:";
    public const string FacilitiesPrefix = "facilities:";

    /// <summary>
    /// Everything derived from a published listing: search pages, details, featured.
    /// Dropped whole on any write to a listing, because working out which cached
    /// search pages held a given listing is not worth being wrong about.
    /// </summary>
    public const string PropertiesPrefix = "properties:";

    /// <summary>Cities, areas, landmarks and facilities change only when the seeder runs.</summary>
    public static readonly TimeSpan ReferenceDataTtl = TimeSpan.FromHours(24);

    public static string Cities() => LocationsPrefix + "cities";

    public static string Areas(int cityId) => $"{LocationsPrefix}areas:{cityId}";

    public static string LocationSuggest(string normalisedQuery) =>
        $"{LocationsPrefix}suggest:{normalisedQuery}";

    public static string Facilities() => FacilitiesPrefix + "all";

    /// <summary>Popular searches repeat many times a day; a stale page costs five minutes.</summary>
    public static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(5);

    /// <summary>A published listing is read far more often than an admin approves a change to it.</summary>
    public static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(15);

    /// <summary>The same eight listings for every visitor, which is what makes this safe.</summary>
    public static readonly TimeSpan FeaturedTtl = TimeSpan.FromMinutes(10);

    /// <summary>Rule 83. The tail comes from <c>PropertySearchCriteria.Describe()</c>.</summary>
    public static string PropertySearch(string filters) => $"{PropertiesPrefix}search:{filters}";

    public static string PropertyDetail(int id) => $"{PropertiesPrefix}detail:{id}";

    public static string PropertyPhotos(int id) => $"{PropertiesPrefix}photos:{id}";

    public static string Featured() => PropertiesPrefix + "featured";

    /// <summary>
    /// Rule 105. Not a cache: a note that an email has already gone out, with an
    /// expiry date and no reader but the sender — the same shape as the OTP request
    /// counter. It names the recipient as well as the thread because the limit in
    /// §10 is about an inbox, so a reply in the other direction is not throttled by
    /// the message it answers.
    /// </summary>
    public static string MessageNudge(int conversationId, int recipientId) =>
        $"nudge:message:{conversationId}:{recipientId}";

    public static readonly TimeSpan MessageNudgeWindow = TimeSpan.FromHours(1);

    /// <summary>
    /// Rule 125's limit, the same shape as the message nudge above. The sweep runs
    /// daily and the email does not: an owner away for a fortnight should come back to
    /// two nudges, not fourteen and a filter rule that hides the fifteenth.
    /// </summary>
    public static string StaleNudge(int propertyId) => $"nudge:stale:{propertyId}";
}
