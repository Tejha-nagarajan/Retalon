using System.Net.Http.Json;
using Retalon.DTOs.Auth;

namespace Retalon.Tests.Infrastructure;

public record TestUser(string Email, string Password, string AccessToken, string RefreshToken);

/// <summary>
/// Registers and logs in a real user via the real /api/auth endpoints (no shortcuts),
/// so integration tests exercise the actual AuthService/TokenService/JWT pipeline.
/// </summary>
public static class AuthTestHelper
{
    private const string DefaultPassword = "P@ssw0rd123!";

    public static async Task<TestUser> RegisterAndLoginAsync(
        HttpClient client,
        string? email = null,
        string password = DefaultPassword)
    {
        email ??= $"user_{Guid.NewGuid():N}@test.local";

        var registerRequest = new RegisterRequestDto
        {
            FirstName = "Test",
            LastName = "User",
            Email = email,
            Password = password,
            PhoneNumber = "5555555555",
            AddressLine1 = "123 Test St",
            City = "Testville",
            State = "TS",
            PostalCode = "00000",
            Country = "USA"
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new LoginRequestDto { Email = email, Password = password };
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        return new TestUser(email, password, auth!.AccessToken, auth.RefreshToken);
    }

    public static void SetBearerToken(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }
}
