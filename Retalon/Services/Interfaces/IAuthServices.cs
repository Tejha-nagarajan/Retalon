using Retalon.DTOs.Auth;

namespace Retalon.Services.Interfaces;

public interface IAuthService
{
    Task<string> RegisterAsync(RegisterRequestDto request);

    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);

    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

    Task LogoutAsync(LogoutRequestDto request);
}