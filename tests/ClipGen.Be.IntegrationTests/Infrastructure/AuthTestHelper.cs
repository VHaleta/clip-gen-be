using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Auth;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

public static class AuthTestHelper
{
    public static async Task RegisterAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(userName, password), TestJson.Options);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password), TestJson.Options);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        return auth!.Token;
    }

    public static async Task<(string UserName, string Token)> RegisterAndLoginAsync(HttpClient client)
    {
        var userName = $"u{Guid.NewGuid():N}"[..20];
        const string password = "Str0ng!Passw0rd";

        await RegisterAsync(client, userName, password);
        var token = await LoginAsync(client, userName, password);

        return (userName, token);
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
