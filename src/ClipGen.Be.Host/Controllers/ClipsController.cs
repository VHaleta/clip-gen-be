using ClipGen.Be.Host.Contracts.Clips;
using ClipGen.Be.Host.Filters;
using ClipGen.Be.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClipGen.Be.Host.Controllers;

public sealed class ClipsController(ClipService clips) : ApiControllerBase
{
    [HttpPost("api/jobs/{id:guid}/clips")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<ClipResponse>> AddClip(Guid id, AddClipRequest request, CancellationToken ct)
    {
        var clip = await clips.AddAsync(CurrentUserId, id, request, ct);
        return CreatedAtAction(nameof(JobsController.Get), "Jobs", new { id }, clip);
    }

    [HttpPut("api/clips/{id:guid}")]
    [ServiceFilter(typeof(ClipOwnershipFilter))]
    public async Task<ActionResult<ClipResponse>> UpdateClip(Guid id, UpdateClipRequest request, CancellationToken ct) =>
        Ok(await clips.UpdateAsync(CurrentUserId, id, request, ct));

    [HttpDelete("api/clips/{id:guid}")]
    [ServiceFilter(typeof(ClipOwnershipFilter))]
    public async Task<IActionResult> DeleteClip(Guid id, CancellationToken ct)
    {
        await clips.DeleteAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}
