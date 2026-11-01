using ClipGen.Be.Host.Contracts.Admin;
using ClipGen.Core.Domain;
using ClipGen.Infrastructure.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClipGen.Be.Host.Controllers;

[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminController(JobReportsQueries reports) : ApiControllerBase
{
    /// <summary>
    /// Кросс-пользовательский отчёт через Dapper (раздел 7 data-layer-plan.md) — только метаданные,
    /// без транскриптов, текстов нарратива или файлов других пользователей (product-spec 3.6).
    /// </summary>
    [HttpGet("jobs-summary")]
    public async Task<ActionResult<IReadOnlyList<AdminJobSummaryResponse>>> JobsSummary(CancellationToken ct)
    {
        var rows = await reports.GetJobsSummaryAsync(ct);
        return Ok(rows.Select(ToResponse).ToList());
    }

    private static AdminJobSummaryResponse ToResponse(JobSummaryRow row) => new(
        row.JobId,
        row.UserName,
        row.OriginalFileName,
        ((JobStatus)row.Status).ToString(),
        row.CreatedAt,
        row.ClipCount,
        string.IsNullOrWhiteSpace(row.Tags)
            ? []
            : row.Tags.Split(", ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
