using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ClipGen.Be.Host.Contracts.Validation;

/// <summary>Имя тега: буквы, цифры, пробел, дефис, до 64 символов (tech-spec раздел 16).</summary>
public sealed partial class TagNamesAttribute : ValidationAttribute
{
    public TagNamesAttribute()
        : base("Тег должен содержать буквы, цифры, пробел или дефис, не более 64 символов.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not IEnumerable<string> names)
            return false;

        return names.All(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 64 && TagRegex().IsMatch(n));
    }

    [GeneratedRegex(@"^[\p{L}0-9 \-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex TagRegex();
}
