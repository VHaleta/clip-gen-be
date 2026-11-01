using ClipGen.Be.Host.Contracts.Clips;
using ClipGen.Be.Host.Filters;
using ClipGen.Core.Domain;
using ClipGen.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Be.Host.Controllers;

public sealed class ClipsController(AppDbContext db) : ApiControllerBase
{
    [HttpPost("api/jobs/{id:guid}/clips")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<ClipResponse>> AddClip(Guid id, AddClipRequest request)
    {
        var job = await db.Jobs
            .Include(j => j.Clips)
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == CurrentUserId);
        if (job is null)
            return NotFound();

        Clip clip;
        try
        {
            var range = new TimeRange(TimeSpan.FromSeconds(request.StartSec), TimeSpan.FromSeconds(request.EndSec));
            clip = job.AddClip(range, request.Source, request.Score, request.Reason, request.Hook);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }

        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(JobsController.Get), "Jobs", new { id = job.Id }, ClipResponse.From(clip));
    }

    [HttpPut("api/clips/{id:guid}")]
    [ServiceFilter(typeof(ClipOwnershipFilter))]
    public async Task<ActionResult<ClipResponse>> UpdateClip(Guid id, UpdateClipRequest request)
    {
        var clip = await db.Clips.Include(c => c.Job).FirstOrDefaultAsync(c => c.Id == id);
        if (clip is null)
            return NotFound();

        try
        {
            var range = new TimeRange(TimeSpan.FromSeconds(request.StartSec), TimeSpan.FromSeconds(request.EndSec));
            clip.ApplyTimecodes(range, clip.Job.SourceDuration);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }

        clip.SetSelected(request.IsSelected);
        clip.SetNarrativeText(request.NarrativeText);

        await db.SaveChangesAsync();

        return Ok(ClipResponse.From(clip));
    }

    [HttpDelete("api/clips/{id:guid}")]
    [ServiceFilter(typeof(ClipOwnershipFilter))]
    public async Task<IActionResult> DeleteClip(Guid id)
    {
        var clip = await db.Clips.FirstOrDefaultAsync(c => c.Id == id);
        if (clip is null)
            return NotFound();

        db.Clips.Remove(clip);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
