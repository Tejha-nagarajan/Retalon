using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Auth;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IAuditService _auditService;
    private readonly ISecurityEventService _securityEventService;

    public AuthService(
        ApplicationDbContext context,
        ITokenService tokenService,
        IConfiguration configuration,
        IAuditService auditService,
        ISecurityEventService securityEventService)
    {
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
        _auditService = auditService;
        _securityEventService = securityEventService;
    }

    public async Task<string> RegisterAsync(RegisterRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == email);

        if (existingUser)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        var customerRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == "Customer");

        if (customerRole == null)
        {
            throw new InvalidOperationException(
                "Customer role has not been configured.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Address = request.AddressLine1.Trim(),
            City = request.City.Trim(),
            PostalCode = request.PostalCode.Trim(),
            Country = request.Country.Trim(),
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedDate = DateTime.UtcNow
        };

        var userRole = new UserRole
        {
            UserId = user.UserId,
            RoleId = customerRole.RoleId
        };

        var cart = new Cart
        {
            CartId = Guid.NewGuid(),
            UserId = user.UserId
        };

        _context.Users.Add(user);
        _context.UserRoles.Add(userRole);
        _context.Carts.Add(cart);

        await _context.SaveChangesAsync();

        return "User registered successfully.";
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException(
                "This account is inactive.");
        }

        if (user.LockedUntil.HasValue &&
            user.LockedUntil.Value > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException(
                "Account is temporarily locked.");
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginAttempts = 0;

                await _context.SaveChangesAsync();

                await _securityEventService.LogAsync(
                    user.UserId,
                    SecurityEventType.AccountLocked,
                    "Account locked after 5 failed login attempts.");
            }
            else
            {
                await _context.SaveChangesAsync();

                await _securityEventService.LogAsync(
                    user.UserId,
                    SecurityEventType.FailedLogin,
                    "Failed login attempt.");
            }

            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginDate = DateTime.UtcNow;

        //Audit
        await _auditService.LogAsync(
            user.UserId,
            "UserLogin",
            "User",
            user.UserId.ToString(),
            "User logged in successfully.");


        var roles = user.UserRoles
            .Select(ur => ur.Role.Name)
            .ToList();

        var accessToken = _tokenService.GenerateAccessToken(
            user.UserId.ToString(),
            user.Email,
            roles);

        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken),
            CreatedDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _context.RefreshTokens.Add(refreshTokenEntity);

        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = GetAccessTokenExpiry()
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(
        RefreshTokenRequestDto request)
    {
        var validTokens = await _context.RefreshTokens
            .Include(rt => rt.User)
            .Where(rt =>
                rt.RevokedDate == null &&
                rt.ExpiryDate > DateTime.UtcNow)
            .ToListAsync();

        RefreshToken? storedToken = null;

        foreach (var token in validTokens)
        {
            if (BCrypt.Net.BCrypt.Verify(
                    request.RefreshToken,
                    token.TokenHash))
            {
                storedToken = token;
                break;
            }
        }

        if (storedToken == null)
        {
            throw new UnauthorizedAccessException(
                "Invalid or expired refresh token.");
        }

        var user = storedToken.User;

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException(
                "This account is inactive.");
        }

        var roles = await _context.UserRoles
            .Where(ur => ur.UserId == user.UserId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        storedToken.RevokedDate = DateTime.UtcNow;

        var newAccessToken = _tokenService.GenerateAccessToken(
            user.UserId.ToString(),
            user.Email,
            roles);

        var newRefreshToken = _tokenService.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(newRefreshToken),
            CreatedDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);

        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            AccessTokenExpiresAt = GetAccessTokenExpiry()
        };
    }

    public async Task LogoutAsync(LogoutRequestDto request)
    {
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.RevokedDate == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            if (BCrypt.Net.BCrypt.Verify(
                    request.RefreshToken,
                    token.TokenHash))
            {
                token.RevokedDate = DateTime.UtcNow;

                await _securityEventService.LogAsync(
                    token.UserId,
                    SecurityEventType.TokenRevoked,
                    "Refresh token revoked during logout.");

                await _context.SaveChangesAsync();

                return;
            }
        }

        throw new UnauthorizedAccessException(
            "Invalid refresh token.");
    }

    private DateTime GetAccessTokenExpiry()
    {
        var minutes = _configuration
            .GetValue<int>("Jwt:AccessTokenExpirationMinutes");

        return DateTime.UtcNow.AddMinutes(minutes);
    }
}