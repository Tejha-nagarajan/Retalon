namespace Retalon.Services.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(string userId, string email, List<string> Roles);
    string GenerateRefreshToken();
}