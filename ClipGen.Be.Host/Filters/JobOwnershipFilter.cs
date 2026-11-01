using System.Security.Claims;
using ClipGen.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Be.Host.Filters;

/// <summary>404, если {id} из маршрута — не Job текущего пользователя (чужие данные не раскрываются).</summary>
public sealed class JobOwnershipFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ActionArguments.TryGetValue("id", out var idObj) || idObj is not Guid jobId)
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

        var exists = await db.Jobs.AnyAsync(j => j.Id == jobId && j.UserId == userId);
        if (!exists)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
