namespace ClipGen.Core.Abstractions.Persistence;

/// <summary>Commits all changes accumulated by repositories within the request in a single transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
