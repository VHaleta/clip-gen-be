using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ClipGen.Be.Host.Contracts.Validation;

/// <summary>Tag name: letters, digits, space, hyphen, up to 64 characters (tech-spec section 16).</summary>
public sealed partial class TagNamesAttribute : ValidationAttribute
{
    public TagNamesAttribute()
        : base("A tag may contain only letters, digits, spaces or hyphens, up to 64 characters.")
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
