using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

public interface IClipRepository
{
    /// <summary>Клип задачи пользователя вместе с задачей (нужна длительность исходника).</summary>
    Task<Clip?> FindWithJobForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<Clip?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    void Remove(Clip clip);
}
