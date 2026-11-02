using System.ComponentModel.DataAnnotations;

namespace ClipGen.Be.Host.Contracts.Validation;

public interface IClipRange
{
    double StartSec { get; }
    double EndSec { get; }
}

/// <summary>Clip length is 1-180 seconds and the end is after the start (tech-spec section 14).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ClipRangeAttribute : ValidationAttribute
{
    public ClipRangeAttribute()
        : base("Clip: the range end must be greater than the start and the duration must be 1 to 180 seconds.")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IClipRange range)
            return ValidationResult.Success;

        var duration = range.EndSec - range.StartSec;
        if (duration is >= 1 and <= 180)
            return ValidationResult.Success;

        return new ValidationResult(ErrorMessage, [nameof(IClipRange.StartSec), nameof(IClipRange.EndSec)]);
    }
}
