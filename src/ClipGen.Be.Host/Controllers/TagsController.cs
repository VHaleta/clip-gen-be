using ClipGen.Be.Host.Contracts.Tags;
using ClipGen.Be.Host.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClipGen.Be.Host.Controllers;

[Route("api/tags")]
public sealed class TagsController(TagService tags) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> List(CancellationToken ct) =>
        Ok(await tags.ListAsync(CurrentUserId, ct));
}
