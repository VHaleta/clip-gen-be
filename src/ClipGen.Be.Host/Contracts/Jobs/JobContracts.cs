using System.ComponentModel.DataAnnotations;
using ClipGen.Be.Host.Contracts.Clips;
using ClipGen.Be.Host.Contracts.Validation;
using ClipGen.Core.Domain;

namespace ClipGen.Be.Host.Contracts.Jobs;

public sealed record CreateJobRequest(
    [Required, StringLength(260, MinimumLength = 1)] string OriginalFileName,
    [Required, RegularExpression("^[a-fA-F0-9]{64}$", ErrorMessage = "SourceFileHash must be a SHA-256 hash (64 hex characters).")]
    string SourceFileHash,
    [Range(0.1, 36000)] double SourceDurationSeconds,
    ProcessingOptionsDto? Options);

public sealed record UpdateJobTagsRequest([TagNames] IReadOnlyList<string> TagNames);

public sealed record JobListItem(Guid Id, string OriginalFileName, string Status, int ClipCount, DateTimeOffset CreatedAt);

public sealed record JobResponse(
    Guid Id,
    string OriginalFileName,
    string SourceFileHash,
    double SourceDurationSeconds,
    string Status,
    ProcessingOptionsDto Options,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ClipResponse> Clips,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static JobResponse From(Job job) => new(
        job.Id,
        job.OriginalFileName,
        job.SourceFileHash,
        job.SourceDuration.TotalSeconds,
        job.Status.ToString(),
        ProcessingOptionsDto.From(job.Options),
        job.Tags.Select(t => t.Name).ToList(),
        job.Clips.OrderBy(c => c.Index).Select(ClipResponse.From).ToList(),
        job.CreatedAt,
        job.UpdatedAt);
}
