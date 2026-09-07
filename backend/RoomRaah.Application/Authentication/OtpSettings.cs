namespace RoomRaah.Application.Authentication;

public class OtpSettings
{
    public int CodeLength { get; set; }
    public int ExpiryMinutes { get; set; }
    public int MaxVerifyAttempts { get; set; }
    public int MaxRequestsPerWindow { get; set; }
    public int RequestWindowMinutes { get; set; }
    public int PasswordResetExpiryMinutes { get; set; }
}
