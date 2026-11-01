using System.Net;
using System.Net.Http.Json;
using ClipGen.Be.Host.Contracts.Jobs;
using ClipGen.Be.IntegrationTests.Infrastructure;
using ClipGen.Core.Domain;

namespace ClipGen.Be.IntegrationTests;

[Collection("Database")]
public sealed class JobStorageTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task CreateAndFetchJob_RoundTripsOptionsIncludingNestedCropSettings()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var options = new ProcessingOptionsDto
        {
            TargetLength = ClipLength.Sec60,
            ClipCount = 7,
            AnalysisLanguage = "ru",
            Crop = new CropSettingsDto { Mode = CropMode.ManualOffset, Offset = 0.25 },
        };

        var created = await JobTestHelper.CreateJobAsync(client, "stream_test.mp4", options);

        var getResponse = await client.GetAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);

        Assert.NotNull(fetched);
        Assert.Equal("stream_test.mp4", fetched!.OriginalFileName);
        Assert.Equal(300, fetched.SourceDurationSeconds, precision: 3);
        Assert.Equal(ClipLength.Sec60, fetched.Options.TargetLength);
        Assert.Equal(7, fetched.Options.ClipCount);
        Assert.Equal("ru", fetched.Options.AnalysisLanguage);
        Assert.Equal(CropMode.ManualOffset, fetched.Options.Crop.Mode);
        Assert.Equal(0.25, fetched.Options.Crop.Offset, precision: 3);
    }

    [Fact]
    public async Task UpdateOptions_PersistsNewValues()
    {
        var client = fixture.Factory.CreateClient();
        var (_, token) = await AuthTestHelper.RegisterAndLoginAsync(client);
        client.WithToken(token);

        var job = await JobTestHelper.CreateJobAsync(client);

        var newOptions = new ProcessingOptionsDto { TargetLength = ClipLength.Sec90, ClipCount = 2, AnalysisLanguage = "en" };
        var updateResponse = await client.PutAsJsonAsync($"/api/jobs/{job.Id}/options", newOptions, TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var fetched = await (await client.GetAsync($"/api/jobs/{job.Id}")).Content.ReadFromJsonAsync<JobResponse>(TestJson.Options);
        Assert.Equal(ClipLength.Sec90, fetched!.Options.TargetLength);
        Assert.Equal(2, fetched.Options.ClipCount);
        Assert.Equal("en", fetched.Options.AnalysisLanguage);
    }
}
