using System.Text.Json;
using FluentValidation;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // The status code decides the log level, so the translation has to happen
            // before the logging rather than after it.
            var response = Translate(context, ex);

            Log(context, ex, response.StatusCode);

            await WriteAsync(context, response);
        }
    }

    private static ErrorResponse Translate(HttpContext context, Exception exception)
    {
        var response = new ErrorResponse
        {
            Success = false,
            TraceId = context.TraceIdentifier
        };

        switch (exception)
        {
            case ValidationException validationException:
                response.StatusCode = StatusCodes.Status400BadRequest;
                response.Error = "One or more validation errors occurred.";
                response.Errors = validationException.Errors
                    .Select(e => new ValidationError
                    {
                        Field = e.PropertyName,
                        Message = e.ErrorMessage
                    })
                    .ToList();
                break;

            case UnauthenticatedException unauthenticatedException:
                response.StatusCode = StatusCodes.Status401Unauthorized;
                response.Error = unauthenticatedException.Message;
                break;

            case UnauthorizedAccessException:
                response.StatusCode = StatusCodes.Status403Forbidden;
                response.Error = "You do not have permission to perform this action.";
                break;

            case NotFoundException notFoundException:
                response.StatusCode = StatusCodes.Status404NotFound;
                response.Error = notFoundException.Message;
                break;

            case BusinessRuleException businessRuleException:
                response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                response.Error = businessRuleException.Message;
                break;

            case RateLimitException rateLimitException:
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Error = rateLimitException.Message;
                response.RetryAfterSeconds = rateLimitException.RetryAfterSeconds;
                context.Response.Headers.RetryAfter = rateLimitException.RetryAfterSeconds.ToString();
                break;

            default:
                response.StatusCode = StatusCodes.Status500InternalServerError;
                response.Error = "An unexpected error occurred.";
                break;
        }

        return response;
    }

    /// <summary>
    /// Logs at the level the answer deserves. Everything above the default case is a
    /// refusal this application decided on, so logging it at Error would fire an
    /// error-rate alert on working software and bury the one line that means a fault.
    /// </summary>
    private void Log(HttpContext context, Exception exception, int statusCode)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", method, path);
            return;
        }

        var level = statusCode is StatusCodes.Status401Unauthorized
            or StatusCodes.Status403Forbidden
            or StatusCodes.Status429TooManyRequests
            ? LogLevel.Warning
            : LogLevel.Information;

        // The exception's own message, not the one in the body: a 403 answers with a
        // fixed sentence so a caller cannot learn from it whether the record exists,
        // which leaves the log as the only place the real reason survives.
        var reason = exception switch
        {
            ValidationException validationException => string.Join(
                "; ",
                validationException.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")),
            _ => exception.Message
        };

        _logger.Log(
            level,
            "Request {Method} {Path} refused with {StatusCode}: {Reason}",
            method,
            path,
            statusCode,
            reason);
    }

    private static async Task WriteAsync(HttpContext context, ErrorResponse response)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
