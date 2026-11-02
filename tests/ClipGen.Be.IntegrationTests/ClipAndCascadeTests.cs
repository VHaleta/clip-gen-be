using System.Net;
using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Clips;
using ClipGen.Be.IntegrationTests.Infrastructure;
using ClipGen.Core.Domain;

namespace ClipGen.Be.IntegrationTests;

[Collection("Database")]
public sealed class ClipAndCascadeTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task AddUpdateDeleteClip_RoundTripsCorrectly()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var job = await JobTestHelper.CreateJobAsync(client);

        var addResponse = await client.PostAsJsonAsync(
            $"/api/jobs/{job.Id}/clips",
            new AddClipRequest(10, 40, ClipSource.Ai, 70, "strong hook", "watch this"),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var clip = await addResponse.Content.ReadFromJsonAsync<ClipResponse>(TestJson.Options);
        Assert.NotNull(clip);
        Assert.Equal(30, clip!.EndSec - clip.StartSec, precision: 3);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/clips/{clip.Id}",
            new UpdateClipRequest(12, 45, false, "edited"),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ClipResponse>(TestJson.Options);
        Assert.False(updated!.IsSelected);
        Assert.Equal("edited", updated.NarrativeText);

        var deleteResponse = await client.DeleteAsync($"/api/clips/{clip.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var reDeleteResponse = await client.DeleteAsync($"/api/clips/{clip.Id}");
        Assert.Equal(HttpStatusCode.NotFound, reDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task AddClip_AfterDeletingEarlierClip_GetsUniqueIndex()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var job = await JobTestHelper.CreateJobAsync(client);

        var indexes = new List<int>();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/jobs/{job.Id}/clips",
                new AddClipRequest(i * 10, i * 10 + 5, ClipSource.Manual, 50, "pick", null),
                TestJson.Options);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var clip = await response.Content.ReadFromJsonAsync<ClipResponse>(TestJson.Options);
            ids.Add(clip!.Id);
            indexes.Add(clip.Index);
        }
        Assert.Equal([0, 1, 2], indexes);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/clips/{ids[0]}")).StatusCode);

        // Count is now 2, which would collide with the existing index 2.
        var addAfterDelete = await client.PostAsJsonAsync(
            $"/api/jobs/{job.Id}/clips",
            new AddClipRequest(40, 45, ClipSource.Manual, 50, "pick", null),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.Created, addAfterDelete.StatusCode);
        var added = await addAfterDelete.Content.ReadFromJsonAsync<ClipResponse>(TestJson.Options);
        Assert.Equal(3, added!.Index);
    }

    [Fact]
    public async Task DeletingJob_CascadesItsClips()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var job = await JobTestHelper.CreateJobAsync(client);
        var addResponse = await client.PostAsJsonAsync(
            $"/api/jobs/{job.Id}/clips",
            new AddClipRequest(5, 20, ClipSource.Manual, 50, "manual pick", null),
            TestJson.Options);
        var clip = await addResponse.Content.ReadFromJsonAsync<ClipResponse>(TestJson.Options);

        var deleteJobResponse = await client.DeleteAsync($"/api/jobs/{job.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteJobResponse.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/jobs/{job.Id}")).StatusCode);

        // The clip was physically cascade-deleted with the job, so the update no longer finds it.
        var updateClipResponse = await client.PutAsJsonAsync(
            $"/api/clips/{clip!.Id}",
            new UpdateClipRequest(1, 2, true, null),
            TestJson.Options);
        Assert.Equal(HttpStatusCode.NotFound, updateClipResponse.StatusCode);
    }
}
