namespace RoomRaah.Application.DTOs;

/// <summary>A paginated result set.</summary>
/// <typeparam name="T">The type of the items in the page.</typeparam>
public class PagedResult<T>
{
    /// <summary>The items on the current page.</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>The current page number (1-based).</summary>
    public int Page { get; set; }

    /// <summary>The number of items per page.</summary>
    public int PageSize { get; set; }
}
