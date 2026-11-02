using System.Security.Claims;
using ClipGen.Core.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClipGen.Be.Host.Filters;

/// <summary>404 if the route {id} is not a Clip of the current user's job.</summary>
public sealed class ClipOwnershipFilter(IClipRepository clips) : IAsyncActionFilter
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

        if (!await clips.ExistsForUserAsync(clipId, userId, context.HttpContext.RequestAborted))
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
