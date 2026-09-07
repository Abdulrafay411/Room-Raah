using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;

namespace RoomRaah.IntegrationTests.Infrastructure;

public static class ApiClientExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        var value = JsonSerializer.Deserialize<T>(body, Json);

        if (value is null)
        {
            throw new InvalidOperationException(
                $"Expected a {typeof(T).Name} but the body was: {body}");
        }

        return value;
    }

    public static async Task<ApiError> ReadErrorAsync(this HttpResponseMessage response)
    {
        return await response.ReadAsync<ApiError>();
    }

    public static void Authorise(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static Task<HttpResponseMessage> PostJsonAsync(
        this HttpClient client, string url, object body)
    {
        return client.PostAsJsonAsync(url, body, Json);
    }

    public static Task<HttpResponseMessage> PutJsonAsync(
        this HttpClient client, string url, object body)
    {
        return client.PutAsJsonAsync(url, body, Json);
    }

    public static Task<HttpResponseMessage> PatchJsonAsync(
        this HttpClient client, string url, object body)
    {
        return client.PatchAsJsonAsync(url, body, Json);
    }

    public static async Task<AuthResponseDto> RegisterAndVerifyAsync(
        this HttpClient client,
        RoomRaahApiFactory factory,
        string email,
        string role = "Seeker",
        string password = "Password1")
    {
        var register = await client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Integration Person",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = password,
            Role = role,
            CnicNumber = role == "Owner" ? "3520112345671" : null
        });

        register.EnsureSuccessStatusCode();

        var code = factory.Emails.LatestSecretFor(email, OtpPurpose.VerifyEmail);

        var verify = await client.PostJsonAsync("/api/v1/auth/verify-otp", new
        {
            Email = email,
            Code = code
        });

        verify.EnsureSuccessStatusCode();

        return await verify.ReadAsync<AuthResponseDto>();
    }
}

/// <summary>Mirror of the API's error envelope, for asserting on failures.</summary>
public class ApiError
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Error { get; set; } = string.Empty;
    public List<ApiFieldError>? Errors { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public string? TraceId { get; set; }
}

public class ApiFieldError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
