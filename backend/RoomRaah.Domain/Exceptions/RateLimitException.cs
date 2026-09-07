namespace RoomRaah.Domain.Exceptions;

public class RateLimitException : Exception
{
    public int RetryAfterSeconds { get; }

    public RateLimitException(string message, int retryAfterSeconds) : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
