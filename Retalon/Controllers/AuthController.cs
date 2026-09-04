using Microsoft.AspNetCore.Mvc;
using Retalon.DTOs.Auth;
using Retalon.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace Retalon.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);

        return Ok(new
        {
            message = result
        });
    }

    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequestDto request)
    {
        var result = await _authService.RefreshTokenAsync(request);

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        LogoutRequestDto request)
    {
        await _authService.LogoutAsync(request);

        return Ok(new
        {
            message = "Logged out successfully."
        });
    }
}