using FluentAssertions;
using Retalon.Services;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

public class AuditServiceTests
{
    [Fact]
    public async Task LogAsync_PersistsAuditLog_WithExpectedFields()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new AuditService(db);
        var userId = Guid.NewGuid();

        await sut.LogAsync(userId, "OrderCreated", "Order", "42", "Order created.");

        var log = db.auditLogs.Single();
        log.UserId.Should().Be(userId);
        log.PerformedByUserId.Should().Be(userId);
        log.Action.Should().Be("OrderCreated");
        log.EntityName.Should().Be("Order");
        log.NewValue.Should().Be("Order created.");
    }

    [Fact]
    public async Task LogAsync_DoesNotPersistEntityId_EvenWhenProvided()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new AuditService(db);

        await sut.LogAsync(Guid.NewGuid(), "SomeAction", entityId: "12345");

        var log = db.auditLogs.Single();
        log.OldValue.Should().BeNull();
        log.NewValue.Should().BeNull();
        log.EntityName.Should().Be(string.Empty);
    }

    [Fact]
    public async Task LogAsync_UsesEmptyEntityName_WhenNotProvided()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new AuditService(db);

        await sut.LogAsync(null, "AnonymousAction");

        var log = db.auditLogs.Single();
        log.UserId.Should().BeNull();
        log.EntityName.Should().Be(string.Empty);
    }
}
