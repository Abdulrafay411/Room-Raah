namespace RoomRaah.Application.Authentication;

/// <summary>
/// Every message an authentication failure can return, defined once.
/// Two different reasons that must look identical to a caller have to share a
/// literal string, and sharing it by accident is not good enough — an attacker who
/// can tell "no such account" from "wrong code" can enumerate accounts.
/// </summary>
public static class AuthMessages
{
    public const string InvalidCredentials = "Invalid email or password.";
    public const string InvalidSession = "This session is no longer valid. Sign in again.";
    public const string InvalidCode = "This code is invalid or has expired. Request a new one.";
    public const string InvalidResetLink = "This link is invalid or has expired. Request a new one.";
    public const string CodeBurned = "This code has been used too many times. Request a new one.";
    public const string AccountDeactivated = "This account has been deactivated and cannot log in.";
    public const string AccountDeactivatedShort = "This account has been deactivated.";

    /// <summary>
    /// Rule 137. Only ever returned after the password has been checked, so the reason is
    /// told to the account's owner and to nobody who merely knows the address.
    /// </summary>
    public static string AccountDeactivatedBecause(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? AccountDeactivated
            : $"This account has been deactivated: {reason.Trim()}";
    public const string EmailAlreadyVerified = "This email address has already been verified.";
    public const string EmailAlreadyTaken = "An account with this email address already exists.";
    public const string AccountNotFound = "No account was found for this email address.";
    public const string RefreshTokenNotFound = "Refresh token was not found.";
}
