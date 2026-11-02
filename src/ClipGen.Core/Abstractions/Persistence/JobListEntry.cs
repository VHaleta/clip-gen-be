using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

/// <summary>Плоская проекция задачи для списка — без загрузки клипов и тегов.</summary>
public sealed record JobListEntry(Guid Id, string OriginalFileName, JobStatus Status, int ClipCount, DateTimeOffset CreatedAt);
