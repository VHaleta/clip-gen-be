namespace ClipGen.Core.Abstractions.Persistence;

/// <summary>Фиксирует все изменения, накопленные репозиториями в рамках запроса, одной транзакцией.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
