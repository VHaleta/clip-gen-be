using System.Data;
using Dapper;

namespace ClipGen.Infrastructure.Reports;

/// <summary>
/// Read-путь через Dapper (micro-ORM): кросс-пользовательский admin-отчёт, которому не нужны ни
/// change tracking EF Core, ни навигации — только плоская проекция. Намеренно не используется
/// LEFT JOIN Clips + LEFT JOIN Tags в одном запросе с STRING_AGG: совместное соединение с двумя
/// независимыми "один-ко-многим" даёт декартово произведение строк и задвоение количества клипов
/// (fan-out). Вместо этого — коррелированные скалярные подзапросы, без дублирования.
/// </summary>
public sealed class JobReportsQueries(IDbConnection connection)
{
    private const string Sql = """
        SELECT
            j.Id AS JobId,
            u.UserName AS UserName,
            j.OriginalFileName,
            j.Status,
            j.CreatedAt,
            (SELECT COUNT(*) FROM Clips c WHERE c.JobId = j.Id) AS ClipCount,
            (SELECT STRING_AGG(t.Name, ', ')
             FROM JobTags jt JOIN Tags t ON t.Id = jt.TagId
             WHERE jt.JobId = j.Id) AS Tags
        FROM Jobs j
        JOIN AspNetUsers u ON u.Id = j.UserId
        ORDER BY j.CreatedAt DESC;
        """;

    public async Task<IReadOnlyList<JobSummaryRow>> GetJobsSummaryAsync(CancellationToken ct)
    {
        var command = new CommandDefinition(Sql, cancellationToken: ct);
        var rows = await connection.QueryAsync<JobSummaryRow>(command);
        return rows.ToList();
    }
}
