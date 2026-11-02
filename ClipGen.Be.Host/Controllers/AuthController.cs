using ClipGen.Be.Host.Contracts.Auth;
using ClipGen.Be.Host.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClipGen.Be.Host.Controllers;

[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request) =>
        StatusCode(StatusCodes.Status201Created, await auth.RegisterAsync(request));

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
        Ok(await auth.LoginAsync(request));

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me() =>
        Ok(await auth.GetMeAsync(CurrentUserId));
}
