using System.ComponentModel.DataAnnotations;
using ClipGen.Be.Host.Contracts.Validation;
using ClipGen.Core.Domain;

namespace ClipGen.Be.Host.Contracts.Clips;

[ClipRange]
public sealed record AddClipRequest(
    double StartSec,
    double EndSec,
    [EnumDataType(typeof(ClipSource))] ClipSource Source,
    [Range(0, 100)] int Score,
    [Required, StringLength(500)] string Reason,
    [StringLength(200)] string? Hook) : IClipRange;

[ClipRange]
public sealed record UpdateClipRequest(
    double StartSec,
    double EndSec,
    bool IsSelected,
    [StringLength(4000)] string? NarrativeText) : IClipRange;

public sealed record ClipResponse(
    Guid Id,
    int Index,
    string Source,
    double StartSec,
    double EndSec,
    int Score,
    string Reason,
    string? Hook,
    bool IsSelected,
    string? NarrativeText)
{
    public static ClipResponse From(Clip clip) => new(
        clip.Id,
        clip.Index,
        clip.Source.ToString(),
        clip.Range.Start.TotalSeconds,
        clip.Range.End.TotalSeconds,
        clip.Score,
        clip.Reason,
        clip.Hook,
        clip.IsSelected,
        clip.NarrativeText);
}
