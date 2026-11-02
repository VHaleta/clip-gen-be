namespace ClipGen.Be.Host.Errors;

/// <summary>
/// Исключения уровня сервисов: сервис сообщает об исходе, а <see cref="ServiceExceptionHandler"/>
/// превращает его в ProblemDetails с нужным HTTP-кодом. Контроллеры не разбирают ошибки сами.
/// </summary>
public abstract class ServiceException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

/// <summary>404 — ресурса нет или он принадлежит другому пользователю (чужие данные не раскрываются).</summary>
public sealed class NotFoundException(string message = "Ресурс не найден.") : ServiceException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>400 — нарушено бизнес-правило (например, диапазон клипа вне исходника).</summary>
public sealed class BusinessRuleException(string message) : ServiceException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>401 — неверные учётные данные.</summary>
public sealed class UnauthorizedException(string message) : ServiceException(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

/// <summary>400 с перечнем ошибок по полям (ValidationProblemDetails).</summary>
public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : ServiceException("Ошибка валидации.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public override int StatusCode => StatusCodes.Status400BadRequest;
}
