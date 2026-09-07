using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoomRaah.API.Middleware;
using RoomRaah.Domain.Exceptions;
using Shouldly;

namespace RoomRaah.UnitTests.Api;

/// <summary>
/// The middleware used to log every exception at Error, including the ones it was
/// written to turn into a 400 or a 422. An alert on error rate would then fire on
/// every business-rule refusal, and the line reading "unhandled" described an
/// exception that is handled by construction. The level now follows the answer the
/// client is given, which is what the build plan section 4.3 asks for.
/// </summary>
public class ExceptionLogLevelTests
{
    private sealed class Recorder : ILogger<ExceptionHandlingMiddleware>
    {
        public record Entry(LogLevel Level, string Message, Exception? Exception);

        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record Refusal(Recorder.Entry Logged, int StatusCode, string Body);

    private static async Task<Refusal> Refuse(Exception thrown)
    {
        var recorder = new Recorder();
        var middleware = new ExceptionHandlingMiddleware(_ => throw thrown, recorder);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/owner/properties";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        recorder.Entries.Count.ShouldBe(1, "The middleware writes exactly one line per exception.");

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return new Refusal(recorder.Entries[0], context.Response.StatusCode, body);
    }

    private static ValidationException MissingTitle() =>
        new([new ValidationFailure("Title", "Title must not be empty.")]);

    [Fact]
    public async Task A_broken_rule_is_information_and_not_an_error()
    {
        var refusal = await Refuse(new BusinessRuleException("A listing needs at least 3 photographs."));

        refusal.StatusCode.ShouldBe(422);
        refusal.Logged.Level.ShouldBe(LogLevel.Information);
        refusal.Logged.Message.ShouldContain("refused with 422");
        refusal.Logged.Message.ShouldContain("A listing needs at least 3 photographs.");
    }

    [Fact]
    public async Task A_record_that_is_not_there_is_information()
    {
        var refusal = await Refuse(new NotFoundException("Property 41 was not found."));

        refusal.StatusCode.ShouldBe(404);
        refusal.Logged.Level.ShouldBe(LogLevel.Information);
        refusal.Logged.Message.ShouldContain("refused with 404");
    }

    [Fact]
    public async Task A_validation_failure_is_information_and_names_the_field()
    {
        var refusal = await Refuse(MissingTitle());

        refusal.StatusCode.ShouldBe(400);
        refusal.Logged.Level.ShouldBe(LogLevel.Information);
        refusal.Logged.Message.ShouldContain("refused with 400");

        // A validation exception's own Message is a multi-line block with a severity
        // on the end of every line, which is unreadable in a log. The fields and the
        // messages are what matter, so they are flattened onto the one line.
        refusal.Logged.Message.ShouldContain("Title: Title must not be empty.");
        refusal.Logged.Message.ShouldNotContain("Severity: Error");
    }

    [Fact]
    public async Task A_rejected_sign_in_is_a_warning()
    {
        var refusal = await Refuse(new UnauthenticatedException("Invalid email or password."));

        refusal.StatusCode.ShouldBe(401);
        refusal.Logged.Level.ShouldBe(LogLevel.Warning);
        refusal.Logged.Message.ShouldContain("refused with 401");
    }

    [Fact]
    public async Task A_forbidden_request_is_a_warning()
    {
        var refusal = await Refuse(new UnauthorizedAccessException("This listing does not belong to you."));

        refusal.StatusCode.ShouldBe(403);
        refusal.Logged.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task The_log_carries_the_reason_the_403_body_withholds()
    {
        var refusal = await Refuse(new UnauthorizedAccessException("This listing does not belong to you."));

        // The body says nothing about why, so a caller cannot use a 403 to find out
        // whether the record exists. The log is the only place the reason survives.
        refusal.Body.ShouldNotContain("does not belong to you");
        refusal.Logged.Message.ShouldContain("This listing does not belong to you.");
    }

    [Fact]
    public async Task A_client_going_too_fast_is_a_warning()
    {
        var refusal = await Refuse(new RateLimitException("Too many attempts. Try again later.", 60));

        refusal.StatusCode.ShouldBe(429);
        refusal.Logged.Level.ShouldBe(LogLevel.Warning);
        refusal.Logged.Message.ShouldContain("refused with 429");
    }

    [Fact]
    public async Task An_unexpected_failure_is_the_only_thing_logged_at_error()
    {
        var refusal = await Refuse(new InvalidOperationException("The database went away."));

        refusal.StatusCode.ShouldBe(500);
        refusal.Logged.Level.ShouldBe(LogLevel.Error);
        refusal.Logged.Message.ShouldBe("Unhandled exception on POST /api/v1/owner/properties");
    }

    [Fact]
    public async Task Only_the_unexpected_failure_is_logged_with_its_stack_trace()
    {
        // The frames under a business-rule refusal say which handler threw, which the
        // path already says. Under a 500 they are the entire point.
        (await Refuse(new BusinessRuleException("No."))).Logged.Exception.ShouldBeNull();
        (await Refuse(new NotFoundException("No."))).Logged.Exception.ShouldBeNull();
        (await Refuse(MissingTitle())).Logged.Exception.ShouldBeNull();
        (await Refuse(new UnauthorizedAccessException("No."))).Logged.Exception.ShouldBeNull();
        (await Refuse(new UnauthenticatedException("No."))).Logged.Exception.ShouldBeNull();
        (await Refuse(new RateLimitException("No.", 60))).Logged.Exception.ShouldBeNull();

        (await Refuse(new InvalidOperationException("No."))).Logged.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_word_unhandled_appears_only_where_nothing_handled_it()
    {
        (await Refuse(new BusinessRuleException("No."))).Logged.Message.ShouldNotContain("Unhandled");
        (await Refuse(new InvalidOperationException("No."))).Logged.Message.ShouldContain("Unhandled");
    }

    [Fact]
    public async Task No_stack_trace_reaches_the_client()
    {
        var refusal = await Refuse(new InvalidOperationException("The database went away."));

        refusal.Body.ShouldNotContain("The database went away");
        refusal.Body.ShouldNotContain("ExceptionLogLevelTests");

        using var document = JsonDocument.Parse(refusal.Body);
        document.RootElement.GetProperty("error").GetString().ShouldBe("An unexpected error occurred.");
    }
}
