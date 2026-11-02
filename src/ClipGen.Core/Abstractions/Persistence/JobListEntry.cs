using ClipGen.Core.Domain;

namespace ClipGen.Core.Abstractions.Persistence;

/// <summary>Flat job projection for the list, without loading clips and tags.</summary>
public sealed record JobListEntry(Guid Id, string OriginalFileName, JobStatus Status, int ClipCount, DateTimeOffset CreatedAt);
