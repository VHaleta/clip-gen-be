using ClipGen.Core.Abstractions.Persistence;
using ClipGen.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Infrastructure.Data.Repositories;

internal sealed class JobRepository(AppDbContext db) : IJobRepository
{
    public Task<Job?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct);

    public Task<Job?> FindWithClipsForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        db.Jobs
            .Include(j => j.Clips)
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct);

    public Task<Job?> FindWithDetailsForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        db.Jobs
            .Include(j => j.Clips)
            .Include(j => j.JobTags).ThenInclude(jt => jt.Tag)
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId, ct);

    public async Task<IReadOnlyList<JobListEntry>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.Jobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobListEntry(j.Id, j.OriginalFileName, j.Status, j.Clips.Count, j.CreatedAt))
            .ToListAsync(ct);

    // ClipCount is a scalar subquery and tags are a separate collection in the projection: EF does not join
    // Clips and Tags in a single JOIN, so there is no Cartesian product and no double-counting of clips.
    public async Task<IReadOnlyList<JobSummary>> ListSummariesForAllUsersAsync(CancellationToken ct = default) =>
        await db.Jobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobSummary(
                j.Id,
                j.User.UserName!,
                j.OriginalFileName,
                j.Status,
                j.CreatedAt,
                j.Clips.Count,
                j.JobTags.Select(jt => jt.Tag.Name).OrderBy(n => n).ToList()))
            .ToListAsync(ct);

    public Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        db.Jobs.AnyAsync(j => j.Id == id && j.UserId == userId, ct);

    public void Add(Job job) => db.Jobs.Add(job);

    // Clips and JobTags are cascade-deleted at the DB level (JobConfiguration) in a single atomic
    // SaveChangesAsync call, without deleting child rows manually.
    public void Remove(Job job) => db.Jobs.Remove(job);
}
