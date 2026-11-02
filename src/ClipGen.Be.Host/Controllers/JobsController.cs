using ClipGen.Be.Host.Contracts.Jobs;
using ClipGen.Be.Host.Filters;
using ClipGen.Be.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClipGen.Be.Host.Controllers;

[Route("api/jobs")]
public sealed class JobsController(JobService jobs) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<JobResponse>> Create(CreateJobRequest request, CancellationToken ct)
    {
        var job = await jobs.CreateAsync(CurrentUserId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, job);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobListItem>>> List(CancellationToken ct) =>
        Ok(await jobs.ListAsync(CurrentUserId, ct));

    [HttpGet("{id:guid}")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<JobResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await jobs.GetAsync(CurrentUserId, id, ct));

    [HttpPut("{id:guid}/options")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<JobResponse>> UpdateOptions(Guid id, ProcessingOptionsDto request, CancellationToken ct) =>
        Ok(await jobs.UpdateOptionsAsync(CurrentUserId, id, request, ct));

    [HttpPut("{id:guid}/tags")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<JobResponse>> UpdateTags(Guid id, UpdateJobTagsRequest request, CancellationToken ct) =>
        Ok(await jobs.UpdateTagsAsync(CurrentUserId, id, request, ct));

    [HttpDelete("{id:guid}")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await jobs.DeleteAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}
