using ClipGen.Core.Abstractions.Persistence;
using ClipGen.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Infrastructure.Data.Repositories;

internal sealed class TagRepository(AppDbContext db) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.Tags
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Tag>> FindByNamesForUserAsync(
        Guid userId, IReadOnlyCollection<string> names, CancellationToken ct = default) =>
        await db.Tags
            .Where(t => t.UserId == userId && names.Contains(t.Name))
            .ToListAsync(ct);

    public void AddRange(IEnumerable<Tag> tags) => db.Tags.AddRange(tags);
}
