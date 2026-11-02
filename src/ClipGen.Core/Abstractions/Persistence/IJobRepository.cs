using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

public interface IJobRepository
{
    /// <summary>The user's job without navigations.</summary>
    Task<Job?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The user's job with clips only.</summary>
    Task<Job?> FindWithClipsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The user's job with clips and tags.</summary>
    Task<Job?> FindWithDetailsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<JobListEntry>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Summary of jobs across all users, for the admin report only.</summary>
    Task<IReadOnlyList<JobSummary>> ListSummariesForAllUsersAsync(CancellationToken ct = default);

    Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    void Add(Job job);

    void Remove(Job job);
}
