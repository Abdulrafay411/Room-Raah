namespace RoomRaah.Application.DTOs;

/// <summary>
/// A listing on someone's shortlist: the public card plus the moment they saved it.
/// Inheriting the public card is the safe direction — a card gains nothing private
/// by being read by the person who saved it, and rule 87 keeps the list to listings
/// that are still published anyway.
/// </summary>
public class SavedListingDto : ListingCardDto
{
    /// <summary>When it went onto the shortlist. The list is ordered by this, newest first.</summary>
    public DateTime SavedAt { get; set; }
}

/// <summary>
/// A visit request as both sides of it see it. Rule 93: the seeker is named by
/// display name, and the owner is not named at all — there is nothing here to reach
/// either of them with off the site, which is the whole reason visit requests exist.
/// </summary>
public class VisitRequestDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The listing the visit is to.</summary>
    public int PropertyId { get; set; }

    /// <summary>Its title, so neither list has to fetch the listing to be readable.</summary>
    public string PropertyTitle { get; set; } = string.Empty;

    /// <summary>The area the listing is in.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>The city that area is in.</summary>
    public string CityName { get; set; } = string.Empty;

    /// <summary>The name the seeker signed up with, and nothing else about them.</summary>
    public string SeekerDisplayName { get; set; } = string.Empty;

    /// <summary>Physical or Video.</summary>
    public string VisitType { get; set; } = string.Empty;

    /// <summary>When the seeker would like to come.</summary>
    public DateTime PreferredAt { get; set; }

    /// <summary>Requested, Accepted, Declined, Completed or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>What the owner wrote back. Required on a decline: rule 89.</summary>
    public string? OwnerResponseNote { get; set; }

    /// <summary>When the owner answered.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>When the request was made. Both lists are ordered by this.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// A review as a stranger reads it. Rule 95 is the whole shape: a display name, the
/// rating, the comment, how close the author got to the room, and the date. There is
/// no author id here, and there is no moderation state — an approved review is the
/// only kind this shape is ever built from.
/// </summary>
public class ReviewDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The listing reviewed.</summary>
    public int PropertyId { get; set; }

    /// <summary>The name the author signed up with.</summary>
    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>One to five.</summary>
    public int Rating { get; set; }

    /// <summary>What they wrote.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Visited, CurrentlyStaying or PreviouslyStayed.</summary>
    public string StayStatus { get; set; } = string.Empty;

    /// <summary>When it was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// One listing's approved reviews, with the average and the count rule 94 asks for.
/// Both are taken over every approved review, not over the page.
/// </summary>
public class PropertyReviewsDto : PagedResult<ReviewDto>
{
    /// <summary>The mean of the approved ratings, or null when there are none.</summary>
    public double? AverageRating { get; set; }
}

/// <summary>
/// A review in the moderation queue. Inherits the public shape rather than the other
/// way round: an admin may see more than a visitor, and a visitor must never come to
/// see more because an admin panel grew.
/// </summary>
public class AdminReviewDto : ReviewDto
{
    /// <summary>Pending, Approved or Hidden.</summary>
    public string ModerationState { get; set; } = string.Empty;

    /// <summary>The listing's title, so the queue is readable without a second call.</summary>
    public string PropertyTitle { get; set; } = string.Empty;

    /// <summary>When an admin last decided about it.</summary>
    public DateTime? ModeratedAt { get; set; }
}

/// <summary>
/// The author's own view of the review they just wrote. It carries the moderation
/// state because the author is the one person entitled to know their review is
/// waiting — rule 41 would otherwise look like the review had vanished.
/// </summary>
public class MyReviewDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The listing reviewed.</summary>
    public int PropertyId { get; set; }

    /// <summary>One to five.</summary>
    public int Rating { get; set; }

    /// <summary>What they wrote.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Visited, CurrentlyStaying or PreviouslyStayed.</summary>
    public string StayStatus { get; set; } = string.Empty;

    /// <summary>Pending until an admin approves it: rule 41.</summary>
    public string ModerationState { get; set; } = string.Empty;

    /// <summary>When it was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// The reporter's own view of a report they filed. Carries no admin note and no
/// admin id: those are the review team's side of the conversation.
/// </summary>
public class MyReportDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The listing reported.</summary>
    public int PropertyId { get; set; }

    /// <summary>Inaccurate, Misleading, AlreadyTaken, Scam, Offensive or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>What they wrote.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Open, UnderReview, Upheld or Dismissed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When it was filed.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>A report in the admin queue, with who filed it and what was decided.</summary>
public class ReportDto : MyReportDto
{
    /// <summary>The listing's title.</summary>
    public string PropertyTitle { get; set; } = string.Empty;

    /// <summary>The name the reporter signed up with, and nothing else about them.</summary>
    public string ReportedByDisplayName { get; set; } = string.Empty;

    /// <summary>What the admin wrote. Required on a verdict: rule 97.</summary>
    public string? AdminNote { get; set; }

    /// <summary>Which admin decided.</summary>
    public int? ResolvedByAdminId { get; set; }

    /// <summary>When they decided.</summary>
    public DateTime? ResolvedAt { get; set; }
}
