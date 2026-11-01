using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Jobs;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

public static class JobTestHelper
{
    public static async Task<JobResponse> CreateJobAsync(
        HttpClient client, string? originalFileName = null, ProcessingOptionsDto? options = null)
    {
        var request = new CreateJobRequest(
            originalFileName ?? $"job_{Guid.NewGuid():N}.mp4",
            new string('a', 64),
            300,
            options);

        var response = await client.PostAsJsonAsync("/api/jobs", request, TestJson.Options);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JobResponse>(TestJson.Options))!;
    }
}
