namespace ClipGen.Be.Host.Contracts.Admin;

/// <summary>Job metadata for the admin report, without narrative text or file paths (product-spec 3.6).</summary>
public sealed record AdminJobSummaryResponse(
    Guid JobId,
    string UserName,
    string OriginalFileName,
    string Status,
    DateTimeOffset CreatedAt,
    int ClipCount,
    IReadOnlyList<string> Tags);
