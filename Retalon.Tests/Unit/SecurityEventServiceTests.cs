using FluentAssertions;
using Retalon.Models.Enums;
using Retalon.Services;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

public class SecurityEventServiceTests
{
    [Fact]
    public async Task LogAsync_PersistsSecurityEvent_WithExpectedFields()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new SecurityEventService(db);
        var userId = Guid.NewGuid();

        await sut.LogAsync(userId, SecurityEventType.FailedLogin, "Bad password.", "127.0.0.1");

        var evt = db.SecurityEvents.Single();
        evt.UserId.Should().Be(userId);
        evt.SecurityEventType.Should().Be(SecurityEventType.FailedLogin);
        evt.Description.Should().Be("Bad password.");
        evt.IpAddress.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task LogAsync_AllowsNullUserIdAndIpAddress()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new SecurityEventService(db);

        await sut.LogAsync(null, SecurityEventType.SuspiciousActivity, "Unknown actor.");

        var evt = db.SecurityEvents.Single();
        evt.UserId.Should().BeNull();
        evt.IpAddress.Should().BeNull();
    }
}
