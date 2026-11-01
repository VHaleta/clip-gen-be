using ClipGen.Be.Host.Contracts.Tags;
using ClipGen.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Be.Host.Controllers;

[Route("api/tags")]
public sealed class TagsController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> List()
    {
        var tags = await db.Tags
            .Where(t => t.UserId == CurrentUserId)
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse(t.Id, t.Name))
            .ToListAsync();

        return Ok(tags);
    }
}
