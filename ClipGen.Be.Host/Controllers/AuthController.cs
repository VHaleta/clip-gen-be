using ClipGen.Be.Host.Auth;
using ClipGen.Be.Host.Contracts.Auth;
using ClipGen.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ClipGen.Be.Host.Controllers;

[Route("api/auth")]
public sealed class AuthController(UserManager<AppUser> userManager, TokenService tokenService) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new AppUser { UserName = request.UserName };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return ValidationProblem(ToModelState(result));

        await userManager.AddToRoleAsync(user, "User");

        return StatusCode(StatusCodes.Status201Created, new { user.Id, user.UserName });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.UserName);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Неверный логин или пароль.");

        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAt) = tokenService.CreateToken(user, roles.ToList());
        return Ok(new AuthResponse(token, expiresAt));
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user is null)
            return NotFound();

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new MeResponse(user.Id, user.UserName!, roles.ToList()));
    }

    private static ModelStateDictionary ToModelState(IdentityResult result)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in result.Errors)
            modelState.AddModelError(error.Code, error.Description);
        return modelState;
    }
}
