using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Retalon.DTOs.Auth;
using Retalon.Models.Entities;
using Retalon.Services;
using Retalon.Services.Interfaces;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<ISecurityEventService> _securityEventService = new();
    private readonly IConfiguration _configuration;

    public AuthServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AccessTokenExpirationMinutes"] = "15"
            })
            .Build();

        _tokenService.Setup(t => t.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
            .Returns("fake-access-token");

        _tokenService.Setup(t => t.GenerateRefreshToken())
            .Returns("fake-refresh-token");
    }

    private AuthService CreateSut(Data.ApplicationDbContext db) =>
        new(db, _tokenService.Object, _configuration, _auditService.Object, _securityEventService.Object);

    private static RegisterRequestDto ValidRegisterRequest(string email = "new.user@test.local") => new()
    {
        FirstName = "New",
        LastName = "User",
        Email = email,
        Password = "P@ssw0rd123!",
        PhoneNumber = "5555555555",
        AddressLine1 = "1 Test St",
        City = "Testville",
        State = "TS",
        PostalCode = "00000",
        Country = "USA"
    };

    [Fact]
    public async Task RegisterAsync_CreatesUserWithCustomerRoleAndCart_WhenEmailNotTaken()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(ValidRegisterRequest());

        result.Should().Be("User registered successfully.");

        var user = db.Users.Single();
        user.Email.Should().Be("new.user@test.local");
        user.IsActive.Should().BeTrue();

        db.UserRoles.Should().ContainSingle(ur => ur.UserId == user.UserId);
        db.Carts.Should().ContainSingle(c => c.UserId == user.UserId);
    }

    [Fact]
    public async Task RegisterAsync_Throws_WhenEmailAlreadyExists()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);
        await sut.RegisterAsync(ValidRegisterRequest("dup@test.local"));

        var act = () => sut.RegisterAsync(ValidRegisterRequest("dup@test.local"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterAsync_Throws_WhenCustomerRoleNotConfigured()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        db.Roles.RemoveRange(db.Roles.Where(r => r.Name == "Customer"));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var act = () => sut.RegisterAsync(ValidRegisterRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Customer role*");
    }

    private async Task<(Data.ApplicationDbContext db, User user, string password)> SeedActiveUserAsync(
        string email = "login@test.local", string password = "P@ssw0rd123!")
    {
        var db = TestDbContextFactory.CreateInMemoryContext();
        var customerRole = db.Roles.Single(r => r.Name == "Customer");

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = "Login",
            LastName = "User",
            Address = "1 Test St",
            City = "Testville",
            PostalCode = "00000",
            Country = "USA",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = customerRole.RoleId });
        await db.SaveChangesAsync();

        return (db, user, password);
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokens_WhenCredentialsValid()
    {
        var (db, user, password) = await SeedActiveUserAsync();
        using var _ = db;
        var sut = CreateSut(db);

        var result = await sut.LoginAsync(new LoginRequestDto { Email = user.Email, Password = password });

        result.AccessToken.Should().Be("fake-access-token");
        result.RefreshToken.Should().Be("fake-refresh-token");
        db.RefreshTokens.Should().ContainSingle(rt => rt.UserId == user.UserId);

        var reloaded = db.Users.Single(u => u.UserId == user.UserId);
        reloaded.FailedLoginAttempts.Should().Be(0);
        reloaded.LastLoginDate.Should().NotBeNull();
    }

    [Fact]
    public async Task LoginAsync_Throws_WhenUserNotFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);

        var act = () => sut.LoginAsync(new LoginRequestDto { Email = "nobody@test.local", Password = "x" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_Throws_AndIncrementsFailedAttempts_WhenPasswordInvalid()
    {
        var (db, user, _) = await SeedActiveUserAsync();
        using var _disp = db;
        var sut = CreateSut(db);

        var act = () => sut.LoginAsync(new LoginRequestDto { Email = user.Email, Password = "WrongPassword!" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        db.Users.Single(u => u.UserId == user.UserId).FailedLoginAttempts.Should().Be(1);
        _securityEventService.Verify(s => s.LogAsync(
            user.UserId,
            Models.Enums.SecurityEventType.FailedLogin,
            It.IsAny<string>(),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_LocksAccount_AfterFiveFailedAttempts()
    {
        var (db, user, _) = await SeedActiveUserAsync();
        using var _disp = db;
        var sut = CreateSut(db);

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => sut.LoginAsync(new LoginRequestDto { Email = user.Email, Password = "WrongPassword!" }));
        }

        var reloaded = db.Users.Single(u => u.UserId == user.UserId);
        reloaded.LockedUntil.Should().NotBeNull();
        reloaded.LockedUntil!.Value.Should().BeAfter(DateTime.UtcNow);
        reloaded.FailedLoginAttempts.Should().Be(0);

        _securityEventService.Verify(s => s.LogAsync(
            user.UserId,
            Models.Enums.SecurityEventType.AccountLocked,
            It.IsAny<string>(),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_Throws_WhenAccountLocked()
    {
        var (db, user, password) = await SeedActiveUserAsync();
        using var _disp = db;
        user.LockedUntil = DateTime.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var act = () => sut.LoginAsync(new LoginRequestDto { Email = user.Email, Password = password });

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*locked*");
    }

    [Fact]
    public async Task LoginAsync_Throws_WhenAccountInactive()
    {
        var (db, user, password) = await SeedActiveUserAsync();
        using var _disp = db;
        user.IsActive = false;
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var act = () => sut.LoginAsync(new LoginRequestDto { Email = user.Email, Password = password });

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*inactive*");
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNewTokens_WhenTokenValid()
    {
        var (db, user, _) = await SeedActiveUserAsync();
        using var _disp = db;

        const string rawRefreshToken = "raw-refresh-token";
        db.RefreshTokens.Add(new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken),
            CreatedDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = rawRefreshToken });

        result.AccessToken.Should().Be("fake-access-token");
        db.RefreshTokens.Count(rt => rt.UserId == user.UserId).Should().Be(2);
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokesOldToken_WhenValid()
    {
        var (db, user, _) = await SeedActiveUserAsync();
        using var _disp = db;

        const string rawRefreshToken = "raw-refresh-token";
        var storedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken),
            CreatedDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };
        db.RefreshTokens.Add(storedToken);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        await sut.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = rawRefreshToken });

        db.RefreshTokens.Single(rt => rt.RefreshTokenId == storedToken.RefreshTokenId)
            .RevokedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_Throws_WhenTokenInvalid()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);

        var act = () => sut.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = "does-not-exist" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RefreshTokenAsync_Throws_WhenTokenExpired()
    {
        var (db, user, _) = await SeedActiveUserAsync();
        using var _disp = db;

        const string rawRefreshToken = "expired-token";
        db.RefreshTokens.Add(new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken),
            CreatedDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddDays(-3)
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var act = () => sut.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = rawRefreshToken });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LogoutAsync_RevokesToken_WhenValid()
    {
        var (db, user, _) = await SeedActiveUserAsync();
        using var _disp = db;

        const string rawRefreshToken = "logout-token";
        var storedToken = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken),
            CreatedDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };
        db.RefreshTokens.Add(storedToken);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        await sut.LogoutAsync(new LogoutRequestDto { RefreshToken = rawRefreshToken });

        db.RefreshTokens.Single(rt => rt.RefreshTokenId == storedToken.RefreshTokenId)
            .RevokedDate.Should().NotBeNull();

        _securityEventService.Verify(s => s.LogAsync(
            user.UserId,
            Models.Enums.SecurityEventType.TokenRevoked,
            It.IsAny<string>(),
            null,
            default), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_Throws_WhenTokenInvalid()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);

        var act = () => sut.LogoutAsync(new LogoutRequestDto { RefreshToken = "not-a-real-token" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
