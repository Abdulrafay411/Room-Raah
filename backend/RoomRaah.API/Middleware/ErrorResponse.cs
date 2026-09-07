namespace RoomRaah.API.Middleware;

public class ErrorResponse
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Error { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public List<ValidationError>? Errors { get; set; }
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
