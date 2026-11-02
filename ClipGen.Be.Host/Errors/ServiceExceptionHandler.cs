using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClipGen.Be.Host.Errors;

public sealed class ServiceExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not ServiceException serviceException)
            return false;

        httpContext.Response.StatusCode = serviceException.StatusCode;

        ProblemDetails details = serviceException is ValidationFailedException validation
            ? new ValidationProblemDetails(validation.Errors)
            : new ProblemDetails { Title = serviceException.Message };
        details.Status = serviceException.StatusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = details,
            Exception = exception,
        });
    }
}
