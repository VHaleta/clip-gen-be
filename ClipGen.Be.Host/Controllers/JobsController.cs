using ClipGen.Be.Host.Contracts.Jobs;
using ClipGen.Be.Host.Filters;
using ClipGen.Core.Domain;
using ClipGen.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Be.Host.Controllers;

[Route("api/jobs")]
public sealed class JobsController(AppDbContext db) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<JobResponse>> Create(CreateJobRequest request)
    {
        Job job;
        try
        {
            job = Job.Create(
                CurrentUserId,
                request.OriginalFileName,
                request.SourceFileHash,
                TimeSpan.FromSeconds(request.SourceDurationSeconds),
                (request.Options ?? new ProcessingOptionsDto()).ToDomain());
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = job.Id }, JobResponse.From(job));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobListItem>>> List()
    {
        var jobs = await db.Jobs
            .Where(j => j.UserId == CurrentUserId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobListItem(j.Id, j.OriginalFileName, j.Status.ToString(), j.Clips.Count, j.CreatedAt))
            .ToListAsync();

        return Ok(jobs);
    }

    [HttpGet("{id:guid}")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<JobResponse>> Get(Guid id)
    {
        var job = await LoadJobAsync(id);
        return job is null ? NotFound() : Ok(JobResponse.From(job));
    }

    [HttpPut("{id:guid}/options")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<JobResponse>> UpdateOptions(Guid id, ProcessingOptionsDto request)
    {
        var job = await LoadJobAsync(id);
        if (job is null)
            return NotFound();

        job.UpdateOptions(request.ToDomain());
        await db.SaveChangesAsync();

        return Ok(JobResponse.From(job));
    }

    [HttpPut("{id:guid}/tags")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<ActionResult<JobResponse>> UpdateTags(Guid id, UpdateJobTagsRequest request)
    {
        var job = await LoadJobAsync(id);
        if (job is null)
            return NotFound();

        var names = request.TagNames
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingTags = await db.Tags
            .Where(t => t.UserId == CurrentUserId && names.Contains(t.Name))
            .ToListAsync();

        var newTags = names
            .Except(existingTags.Select(t => t.Name), StringComparer.OrdinalIgnoreCase)
            .Select(n => Tag.Create(CurrentUserId, n))
            .ToList();

        if (newTags.Count > 0)
            db.Tags.AddRange(newTags);

        job.ReplaceTags(existingTags.Concat(newTags));
        await db.SaveChangesAsync();

        return Ok(JobResponse.From(job));
    }

    [HttpDelete("{id:guid}")]
    [ServiceFilter(typeof(JobOwnershipFilter))]
    public async Task<IActionResult> Delete(Guid id)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == CurrentUserId);
        if (job is null)
            return NotFound();

        // Clips и JobTags удаляются каскадно на уровне БД (JobConfiguration) — одной атомарной
        // операцией SaveChangesAsync, без ручного удаления дочерних строк.
        db.Jobs.Remove(job);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<Job?> LoadJobAsync(Guid id) =>
        await db.Jobs
            .Include(j => j.Clips)
            .Include(j => j.JobTags).ThenInclude(jt => jt.Tag)
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == CurrentUserId);
}
