using System.Security.Claims;
using ClipGen.Core.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClipGen.Be.Host.Filters;

/// <summary>404 if the route {id} is not a Job of the current user (other users' data is not disclosed).</summary>
public sealed class JobOwnershipFilter(IJobRepository jobs) : IAsyncActionFilter
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

        if (!await jobs.ExistsForUserAsync(jobId, userId, context.HttpContext.RequestAborted))
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
