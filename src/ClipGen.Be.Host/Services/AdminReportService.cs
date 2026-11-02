using ClipGen.Be.Host.Contracts.Admin;
using ClipGen.Core.Abstractions.Persistence;

namespace ClipGen.Be.Host.Services;

/// <summary>
/// Cross-user report: metadata only, without transcripts, narrative texts
/// or other users' files (product-spec 3.6).
/// </summary>
public sealed class AdminReportService(IJobRepository jobs)
{
    public async Task<IReadOnlyList<AdminJobSummaryResponse>> GetJobsSummaryAsync(CancellationToken ct)
    {
        var summaries = await jobs.ListSummariesForAllUsersAsync(ct);
        return summaries
            .Select(s => new AdminJobSummaryResponse(
                s.JobId, s.UserName, s.OriginalFileName, s.Status.ToString(), s.CreatedAt, s.ClipCount, s.Tags))
            .ToList();
    }
}
