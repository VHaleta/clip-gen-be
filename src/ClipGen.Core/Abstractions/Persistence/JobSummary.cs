using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

/// <summary>
/// Строка admin-отчёта: только метаданные задачи (без текста нарратива и путей к файлам),
/// по всем пользователям (product-spec 3.6).
/// </summary>
public sealed record JobSummary(
    Guid JobId,
    string UserName,
    string OriginalFileName,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    int ClipCount,
    IReadOnlyList<string> Tags);
