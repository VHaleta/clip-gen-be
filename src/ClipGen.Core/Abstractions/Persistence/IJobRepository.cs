using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

public interface IJobRepository
{
    /// <summary>Задача пользователя без навигаций.</summary>
    Task<Job?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Задача пользователя только с клипами.</summary>
    Task<Job?> FindWithClipsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Задача пользователя с клипами и тегами.</summary>
    Task<Job?> FindWithDetailsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<JobListEntry>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Сводка по задачам всех пользователей — только для админ-отчёта.</summary>
    Task<IReadOnlyList<JobSummary>> ListSummariesForAllUsersAsync(CancellationToken ct = default);

    Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    void Add(Job job);

    void Remove(Job job);
}
