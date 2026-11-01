using System.Net;
using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Jobs;
using ClipGen.Be.Host.Contracts.Tags;
using ClipGen.Be.IntegrationTests.Infrastructure;

namespace ClipGen.Be.IntegrationTests;

[Collection("Database")]
public sealed class TagSharingTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task SharedTag_SurvivesDeletionOfOneOfTwoJobs()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var jobA = await JobTestHelper.CreateJobAsync(client);
        var jobB = await JobTestHelper.CreateJobAsync(client);
        var tagName = $"tag-{Guid.NewGuid():N}"[..20];

        await client.PutAsJsonAsync($"/api/jobs/{jobA.Id}/tags", new UpdateJobTagsRequest([tagName]), TestJson.Options);
        await client.PutAsJsonAsync($"/api/jobs/{jobB.Id}/tags", new UpdateJobTagsRequest([tagName]), TestJson.Options);

        var deleteResponse = await client.DeleteAsync($"/api/jobs/{jobA.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var jobBAfter = await (await client.GetAsync($"/api/jobs/{jobB.Id}")).Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        Assert.Contains(tagName, jobBAfter!.Tags);

        var tags = await (await client.GetAsync("/api/tags")).Content.ReadFromJsonAsync<List<TagResponse>>(TestJson.Options);
        Assert.Contains(tags!, t => t.Name == tagName);
    }

    [Fact]
    public async Task ReplaceTags_KeepsSharedDropsRemovedAddsNew()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var job = await JobTestHelper.CreateJobAsync(client);
        var keep = $"keep-{Guid.NewGuid():N}"[..20];
        var drop = $"drop-{Guid.NewGuid():N}"[..20];
        var add = $"add-{Guid.NewGuid():N}"[..20];

        await client.PutAsJsonAsync($"/api/jobs/{job.Id}/tags", new UpdateJobTagsRequest([keep, drop]), TestJson.Options);
        var secondResponse = await client.PutAsJsonAsync($"/api/jobs/{job.Id}/tags", new UpdateJobTagsRequest([keep, add]), TestJson.Options);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var job2 = await secondResponse.Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        Assert.Equal(new[] { keep, add }.OrderBy(t => t), job2!.Tags.OrderBy(t => t));
    }
}
