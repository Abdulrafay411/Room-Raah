namespace RoomRaah.Application.DTOs;

/// <summary>
/// Returned by login and refresh. The access token is short lived; the refresh
/// token is rotated on every use and revoked on logout.
/// </summary>
public class AuthResponseDto
{
    /// <summary>JWT bearer token for the Authorization header.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Opaque token used to obtain a new access token.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Lifetime of the access token in seconds.</summary>
    public int ExpiresInSeconds { get; set; }

    /// <summary>The account the tokens belong to.</summary>
    public UserSummaryDto User { get; set; } = new();
}
