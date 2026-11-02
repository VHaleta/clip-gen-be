using System.ComponentModel.DataAnnotations;
using ClipGen.Core.Domain;

namespace ClipGen.Be.Host.Contracts.Jobs;

public sealed class CropSettingsDto
{
    [EnumDataType(typeof(CropMode))]
    public CropMode Mode { get; init; } = CropMode.BlurredBackground;

    public double Offset { get; init; } = 0.5;

    public CropSettings ToDomain() => new() { Mode = Mode, Offset = Offset };

    public static CropSettingsDto From(CropSettings crop) => new() { Mode = crop.Mode, Offset = crop.Offset };
}

public sealed class ProcessingOptionsDto : IValidatableObject
{
    [EnumDataType(typeof(ClipLength))]
    public ClipLength TargetLength { get; init; } = ClipLength.Sec30;

    [Range(1, 10)]
    public int ClipCount { get; init; } = 5;

    [Required, StringLength(16, MinimumLength = 2)]
    public string AnalysisLanguage { get; init; } = "auto";

    public CropSettingsDto Crop { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Crop.Mode == CropMode.ManualOffset && Crop.Offset is < 0 or > 1)
        {
            yield return new ValidationResult(
                "Crop.Offset must be within 0..1 in ManualOffset mode.",
                [$"{nameof(Crop)}.{nameof(Crop.Offset)}"]);
        }
    }

    public ProcessingOptions ToDomain() => new()
    {
        TargetLength = TargetLength,
        ClipCount = ClipCount,
        AnalysisLanguage = AnalysisLanguage,
        Crop = Crop.ToDomain(),
    };

    public static ProcessingOptionsDto From(ProcessingOptions options) => new()
    {
        TargetLength = options.TargetLength,
        ClipCount = options.ClipCount,
        AnalysisLanguage = options.AnalysisLanguage,
        Crop = CropSettingsDto.From(options.Crop),
    };
}
