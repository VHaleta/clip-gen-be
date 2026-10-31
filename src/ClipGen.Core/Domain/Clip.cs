namespace ClipGen.Core.Domain;

public class Clip
{
    private static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(180);

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int Index { get; private set; }
    public ClipSource Source { get; private set; }
    public TimeRange Range { get; private set; }
    public int Score { get; private set; }
    public string Reason { get; private set; } = "";
    public string? Hook { get; private set; }
    public bool IsSelected { get; private set; } = true;
    public string? NarrativeText { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Job Job { get; private set; } = default!;

    private Clip()
    {
    }

    internal static Clip Create(
        Guid jobId, int index, TimeRange range, TimeSpan sourceDuration,
        ClipSource source, int score, string reason, string? hook)
    {
        ValidateRange(range, sourceDuration);

        var now = DateTimeOffset.UtcNow;
        return new Clip
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Index = index,
            Source = source,
            Range = range,
            Score = score,
            Reason = reason,
            Hook = hook,
            IsSelected = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void ApplyTimecodes(TimeRange range, TimeSpan sourceDuration)
    {
        ValidateRange(range, sourceDuration);
        Range = range;
        Touch();
    }

    public void SetSelected(bool isSelected)
    {
        IsSelected = isSelected;
        Touch();
    }

    public void SetNarrativeText(string? text)
    {
        NarrativeText = text;
        Touch();
    }

    private static void ValidateRange(TimeRange range, TimeSpan sourceDuration)
    {
        if (range.Duration < MinDuration || range.Duration > MaxDuration)
            throw new ArgumentOutOfRangeException(nameof(range), "Длина клипа должна быть от 1 до 180 секунд.");
        if (range.Start < TimeSpan.Zero || range.End > sourceDuration)
            throw new ArgumentOutOfRangeException(nameof(range), "Диапазон выходит за пределы исходника.");
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
