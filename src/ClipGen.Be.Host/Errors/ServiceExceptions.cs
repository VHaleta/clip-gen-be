namespace ClipGen.Be.Host.Errors;

/// <summary>
/// Service-level exceptions: the service reports the outcome and <see cref="ServiceExceptionHandler"/>
/// turns it into ProblemDetails with the right HTTP status. Controllers do not handle errors themselves.
/// </summary>
public abstract class ServiceException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

/// <summary>404 — the resource does not exist or belongs to another user (other users' data is not disclosed).</summary>
public sealed class NotFoundException(string message = "Resource not found.") : ServiceException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>400 — a business rule is violated (e.g. the clip range is outside the source).</summary>
public sealed class BusinessRuleException(string message) : ServiceException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>401 — invalid credentials.</summary>
public sealed class UnauthorizedException(string message) : ServiceException(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

/// <summary>400 with per-field errors (ValidationProblemDetails).</summary>
public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : ServiceException("Validation failed.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public override int StatusCode => StatusCodes.Status400BadRequest;
}
