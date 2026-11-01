using System.ComponentModel.DataAnnotations;

namespace ClipGen.Be.Host.Contracts.Validation;

public interface IClipRange
{
    double StartSec { get; }
    double EndSec { get; }
}

/// <summary>Длина клипа — 1-180 секунд, конец после начала (tech-spec раздел 14).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ClipRangeAttribute : ValidationAttribute
{
    public ClipRangeAttribute()
        : base("Клип: конец диапазона должен быть больше начала, длительность — от 1 до 180 секунд.")
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
