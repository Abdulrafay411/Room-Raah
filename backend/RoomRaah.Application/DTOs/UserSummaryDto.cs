namespace RoomRaah.Application.DTOs;

/// <summary>
/// The signed-in person's own account summary. Never used in a public response.
/// A phone number is deliberately absent: no DTO in this product carries one.
/// </summary>
public class UserSummaryDto
{
    /// <summary>Account identifier.</summary>
    public int Id { get; set; }

    /// <summary>Display name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Seeker, Owner or Admin.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>False until the emailed code has been verified.</summary>
    public bool IsEmailVerified { get; set; }
}
