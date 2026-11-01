using System.Security.Claims;
using ClipGen.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Be.Host.Filters;

/// <summary>404, если {id} из маршрута — не Clip задачи текущего пользователя.</summary>
public sealed class ClipOwnershipFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ActionArguments.TryGetValue("id", out var idObj) || idObj is not Guid clipId)
        {
            await next();
            return;
        }

        var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var exists = await db.Clips.AnyAsync(c => c.Id == clipId && c.Job.UserId == userId);
        if (!exists)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
