using System.ComponentModel.DataAnnotations;
using ClipGen.Be.Host.Contracts.Validation;

namespace ClipGen.Be.Host.Contracts.Auth;

public sealed record RegisterRequest(
    [Required, UserLogin] string UserName,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public sealed record LoginRequest(
    [Required] string UserName,
    [Required] string Password);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record MeResponse(Guid Id, string UserName, IReadOnlyList<string> Roles);
