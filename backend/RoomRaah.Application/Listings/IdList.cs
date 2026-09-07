namespace RoomRaah.Application.Listings;

/// <summary>
/// The proposed facility list and the proposed gallery ride on a revision as ordered
/// comma-separated ids. Parsing and formatting live here so the two sides can never
/// disagree about the separator or about what an empty list looks like.
/// </summary>
public static class IdList
{
    public static List<int> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToList();
    }

    /// <summary>
    /// The same list arriving from a query string, where it is written by a client
    /// rather than by this product and may be anything at all. Parse throws on
    /// nonsense, which is right for a value this code wrote and wrong for a value a
    /// stranger typed into a URL.
    /// </summary>
    public static bool TryParse(string? value, out List<int> ids)
    {
        ids = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id))
            {
                ids = [];
                return false;
            }

            ids.Add(id);
        }

        return true;
    }

    public static string Format(IEnumerable<int> ids) => string.Join(',', ids);
}
