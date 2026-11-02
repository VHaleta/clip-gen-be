using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Tag>> FindByNamesForUserAsync(Guid userId, IReadOnlyCollection<string> names, CancellationToken ct = default);

    void AddRange(IEnumerable<Tag> tags);
}
