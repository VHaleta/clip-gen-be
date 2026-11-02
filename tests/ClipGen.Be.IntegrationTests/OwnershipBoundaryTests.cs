using System.Net;
using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Jobs;
using ClipGen.Be.IntegrationTests.Infrastructure;

namespace ClipGen.Be.IntegrationTests;

[Collection("Database")]
public sealed class OwnershipBoundaryTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task OtherUser_CannotReadUpdateOrDelete_SomeonesJob()
    {
        var ownerClient = fixture.Factory.CreateClient();
        var (_, ownerToken) = await AuthTestHelper.RegisterAndLoginAsync(ownerClient);
        ownerClient.WithToken(ownerToken);
        var job = await JobTestHelper.CreateJobAsync(ownerClient);

        var strangerClient = fixture.Factory.CreateClient();
        var (_, strangerToken) = await AuthTestHelper.RegisterAndLoginAsync(strangerClient);
        strangerClient.WithToken(strangerToken);

        Assert.Equal(HttpStatusCode.NotFound, (await strangerClient.GetAsync($"/api/jobs/{job.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await strangerClient.PutAsJsonAsync($"/api/jobs/{job.Id}/options", new ProcessingOptionsDto(), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await strangerClient.DeleteAsync($"/api/jobs/{job.Id}")).StatusCode);

        // Another user's job list does not contain this user's job.
        var strangerJobs = await (await strangerClient.GetAsync("/api/jobs")).Content.ReadFromJsonAsync<List<JobListItem>>(TestJson.Options);
        Assert.DoesNotContain(strangerJobs!, j => j.Id == job.Id);

        // The owner still sees the job unchanged: the other user's attempt had no effect.
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/api/jobs/{job.Id}")).StatusCode);
    }
}
