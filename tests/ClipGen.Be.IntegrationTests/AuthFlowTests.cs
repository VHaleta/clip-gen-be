using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Auth;
using ClipGen.Be.IntegrationTests.Infrastructure;

namespace ClipGen.Be.IntegrationTests;

[Collection("Database")]
public sealed class AuthFlowTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Register_Login_Me_ReturnsUserWithUserRole()
    {
        var client = fixture.Factory.CreateClient();
        var (userName, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(TestJson.Options);
        Assert.Equal(userName, me!.UserName);
        Assert.Equal(["User"], me.Roles);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTamperedToken_Returns401()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token + "tampered");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
