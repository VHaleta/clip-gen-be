using ClipGen.Be.Host.Contracts.Tags;
using ClipGen.Core.Abstractions.Persistence;

namespace ClipGen.Be.Host.Services;

public sealed class TagService(ITagRepository tags)
{
    public async Task<IReadOnlyList<TagResponse>> ListAsync(Guid userId, CancellationToken ct)
    {
        var userTags = await tags.ListForUserAsync(userId, ct);
        return userTags.Select(t => new TagResponse(t.Id, t.Name)).ToList();
    }
}
