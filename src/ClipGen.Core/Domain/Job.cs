namespace ClipGen.Core.Domain;

public class Job
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string OriginalFileName { get; private set; } = "";
    public string SourceFileHash { get; private set; } = "";
    public TimeSpan SourceDuration { get; private set; }
    public JobStatus Status { get; private set; }
    public ProcessingOptions Options { get; private set; } = new();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public AppUser User { get; private set; } = default!;
    public List<Clip> Clips { get; private set; } = [];
    public List<JobTag> JobTags { get; private set; } = [];

    public IReadOnlyList<Tag> Tags => JobTags.Select(jt => jt.Tag).ToList();

    private Job()
    {
    }

    public static Job Create(
        Guid userId, string originalFileName, string sourceFileHash, TimeSpan sourceDuration, ProcessingOptions options)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Имя файла обязательно.", nameof(originalFileName));
        if (string.IsNullOrWhiteSpace(sourceFileHash))
            throw new ArgumentException("Хэш исходника обязателен.", nameof(sourceFileHash));
        if (sourceDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sourceDuration), "Длительность исходника должна быть положительной.");

        var now = DateTimeOffset.UtcNow;
        return new Job
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OriginalFileName = originalFileName,
            SourceFileHash = sourceFileHash,
            SourceDuration = sourceDuration,
            Status = JobStatus.Draft,
            Options = options,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void UpdateOptions(ProcessingOptions options)
    {
        Options = options;
        Touch();
    }

    public void Transition(JobStatus next)
    {
        var allowed = Status switch
        {
            JobStatus.Draft => next is JobStatus.AwaitingReview or JobStatus.Cancelled,
            JobStatus.AwaitingReview => next is JobStatus.Done or JobStatus.Cancelled,
            _ => false,
        };
        if (!allowed)
            throw new InvalidOperationException($"Недопустимый переход {Status} -> {next}.");

        Status = next;
        Touch();
    }

    public Clip AddClip(TimeRange range, ClipSource source, int score, string reason, string? hook)
    {
        var clip = Clip.Create(Id, Clips.Count, range, SourceDuration, source, score, reason, hook);
        Clips.Add(clip);
        Touch();
        return clip;
    }

    public void ReplaceTags(IEnumerable<Tag> tags)
    {
        // Мутация существующей коллекции (Add/RemoveAll), а не присвоение нового списка:
        // EF Core отследит именно разницу, без конфликта отслеживания по дублирующемуся
        // составному ключу между старым и новым JobTag с одинаковыми JobId/TagId.
        var desiredIds = tags.Select(t => t.Id).ToHashSet();

        JobTags.RemoveAll(jt => !desiredIds.Contains(jt.TagId));

        var existingIds = JobTags.Select(jt => jt.TagId).ToHashSet();
        foreach (var tag in tags)
        {
            if (!existingIds.Contains(tag.Id))
                JobTags.Add(new JobTag(Id, tag.Id, tag));
        }

        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
