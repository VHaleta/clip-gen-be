using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

public interface IClipRepository
{
    /// <summary>A clip of the user's job, loaded together with the job (the source duration is needed).</summary>
    Task<Clip?> FindWithJobForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<Clip?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    void Remove(Clip clip);
}
