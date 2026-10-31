namespace ClipGen.Core.Domain;

public readonly record struct TimeRange(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;

    public void Deconstruct(out TimeSpan start, out TimeSpan end) => (start, end) = (Start, End);
}

public sealed record CropSettings
{
    public CropMode Mode { get; init; } = CropMode.BlurredBackground;
    public double Offset { get; init; } = 0.5; // 0..1, только для ManualOffset
}

public sealed record ProcessingOptions
{
    public ClipLength TargetLength { get; init; } = ClipLength.Sec30;
    public int ClipCount { get; init; } = 5;
    public string AnalysisLanguage { get; init; } = "auto";
    public CropSettings Crop { get; init; } = new();
}
