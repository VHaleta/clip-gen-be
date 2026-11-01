using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ClipGen.Be.Host.Contracts.Validation;

/// <summary>Логин: латиница, цифры, '.', '_', '-', 3-32 символа; зарезервированные имена запрещены.</summary>
public sealed partial class UserLoginAttribute : ValidationAttribute
{
    private static readonly string[] ReservedNames = ["admin", "root", "system"];

    public UserLoginAttribute()
        : base("Логин должен содержать 3-32 символа: латиница, цифры, '.', '_', '-'.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not string userName)
            return false;

        return LoginRegex().IsMatch(userName) && !ReservedNames.Contains(userName.ToLowerInvariant());
    }

    [GeneratedRegex("^[a-zA-Z0-9._-]{3,32}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex LoginRegex();
}
