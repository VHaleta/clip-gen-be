using ClipGen.Core.Abstractions.Persistence;

namespace ClipGen.Infrastructure.Data.Repositories;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
