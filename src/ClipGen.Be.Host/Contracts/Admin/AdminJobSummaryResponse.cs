namespace ClipGen.Be.Host.Contracts.Admin;

/// <summary>Метаданные задачи для админ-отчёта — без текста нарратива и путей к файлам (product-spec 3.6).</summary>
public sealed record AdminJobSummaryResponse(
    Guid JobId,
    string UserName,
    string OriginalFileName,
    string Status,
    DateTimeOffset CreatedAt,
    int ClipCount,
    IReadOnlyList<string> Tags);
