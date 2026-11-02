using ClipGen.Core.Abstractions.Persistence;
using ClipGen.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Infrastructure.Data.Repositories;

internal sealed class ClipRepository(AppDbContext db) : IClipRepository
{
    public Task<Clip?> FindWithJobForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        db.Clips
            .Include(c => c.Job)
            .FirstOrDefaultAsync(c => c.Id == id && c.Job.UserId == userId, ct);

    public Task<Clip?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        db.Clips.FirstOrDefaultAsync(c => c.Id == id && c.Job.UserId == userId, ct);

    public Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        db.Clips.AnyAsync(c => c.Id == id && c.Job.UserId == userId, ct);

    public void Remove(Clip clip) => db.Clips.Remove(clip);
}
