using System.Net;
using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Admin;
using ClipGen.Be.IntegrationTests.Infrastructure;

namespace ClipGen.Be.IntegrationTests;

[Collection("Database")]
public sealed class AdminReportTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task RegularUser_Gets403_OnAdminReport()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var response = await client.GetAsync("/api/admin/jobs-summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCaller_Gets401_OnAdminReport()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/admin/jobs-summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_SeesJobAcrossUsers_ViaDapperReport()
    {
        var ownerClient = fixture.Factory.CreateClient();
        var (ownerUserName, ownerToken) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        ownerClient.WithToken(ownerToken);
        var job = await JobTestHelper.CreateJobAsync(ownerClient);

        var adminClient = fixture.Factory.CreateClient();
        var adminToken = await AuthTestHelper.LoginAsync(
            adminClient, CustomWebApplicationFactory.AdminUserName, CustomWebApplicationFactory.AdminPassword);
        adminClient.WithToken(adminToken);

        var response = await adminClient.GetAsync("/api/admin/jobs-summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rows = await response.Content.ReadFromJsonAsync<List<AdminJobSummaryResponse>>(TestJson.Options);
        Assert.Contains(rows!, r => r.JobId == job.Id && r.UserName == ownerUserName);
    }
}
