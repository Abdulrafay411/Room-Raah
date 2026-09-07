namespace RoomRaah.Application.Common;

/// <summary>
/// Whether a piece of text names a value an enumeration actually has.
///
/// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> alone is not that
/// question. It accepts <c>"5"</c> for an enumeration with three values and hands
/// back <c>(VisitType)5</c> — a value no switch matches, that no rule refuses, and
/// that comes back out of the API as the number 5. Before this phase every such
/// value was only ever read; a visit type, a stay status and a report reason are the
/// first that a request can write into the database, which is where a number nobody
/// named stops being harmless.
///
/// Every enumeration a client is allowed to name goes through here.
/// </summary>
public static class EnumName
{
    public static bool IsDefined<TEnum>(string? value) where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out _);

    public static bool TryParse<TEnum>(string? value, out TEnum parsed) where TEnum : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }

    /// <summary>The value, or null when the text names nothing the enumeration has.</summary>
    public static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out var parsed) ? parsed : null;
}
