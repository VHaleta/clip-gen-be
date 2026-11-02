using ClipGen.Be.Host.Auth;
using ClipGen.Be.Host.Contracts.Auth;
using ClipGen.Be.Host.Errors;
using ClipGen.Core.Domain;
using Microsoft.AspNetCore.Identity;

namespace ClipGen.Be.Host.Services;

public sealed record RegisteredUser(Guid Id, string UserName);

/// <summary>Пользователи хранятся через ASP.NET Identity — <see cref="UserManager{TUser}"/> играет роль репозитория.</summary>
public sealed class AuthService(UserManager<AppUser> userManager, TokenService tokenService)
{
    public async Task<RegisteredUser> RegisterAsync(RegisterRequest request)
    {
        var user = new AppUser { UserName = request.UserName };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ValidationFailedException(ToErrors(result));

        await userManager.AddToRoleAsync(user, "User");

        return new RegisteredUser(user.Id, user.UserName);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.UserName);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedException("Неверный логин или пароль.");

        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAt) = tokenService.CreateToken(user, roles.ToList());
        return new AuthResponse(token, expiresAt);
    }

    public async Task<MeResponse> GetMeAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException();

        var roles = await userManager.GetRolesAsync(user);
        return new MeResponse(user.Id, user.UserName!, roles.ToList());
    }

    private static Dictionary<string, string[]> ToErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
