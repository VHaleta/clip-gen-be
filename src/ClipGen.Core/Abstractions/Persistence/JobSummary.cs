using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

/// <summary>
/// Admin report row: job metadata only (no narrative text or file paths),
/// across all users (product-spec 3.6).
/// </summary>
public sealed record JobSummary(
    Guid JobId,
    string UserName,
    string OriginalFileName,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    int ClipCount,
    IReadOnlyList<string> Tags);
