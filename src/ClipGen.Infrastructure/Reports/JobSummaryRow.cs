namespace ClipGen.Infrastructure.Reports;

/// <summary>
/// Строка admin-отчёта: только метаданные задачи (без текста нарратива и путей к файлам),
/// по всем пользователям — для /api/admin/jobs-summary.
/// </summary>
public sealed record JobSummaryRow(
    Guid JobId,
    string UserName,
    string OriginalFileName,
    int Status,
    DateTimeOffset CreatedAt,
    int ClipCount,
    string? Tags);
