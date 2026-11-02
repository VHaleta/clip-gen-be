using ClipGen.Be.Host.Contracts.Admin;
using ClipGen.Be.Host.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClipGen.Be.Host.Controllers;

[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminController(AdminReportService reports) : ApiControllerBase
{
    [HttpGet("jobs-summary")]
    public async Task<ActionResult<IReadOnlyList<AdminJobSummaryResponse>>> JobsSummary(CancellationToken ct) =>
        Ok(await reports.GetJobsSummaryAsync(ct));
}
