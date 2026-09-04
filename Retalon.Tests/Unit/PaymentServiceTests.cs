using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Retalon.DTOs.Payments;
using Retalon.Models.Configuration;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services;
using Retalon.Services.Interfaces;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

/// <summary>
/// PaymentService talks directly to the Stripe SDK with no injected abstraction
/// (new PaymentIntentService(), static StripeConfiguration.ApiKey), so only the
/// deterministic guard-clause paths and the local webhook-signature/DB logic are
/// unit-testable here. Stripe API success paths (CreatePaymentIntentAsync's actual
/// PaymentIntentService.CreateAsync/ConfirmAsync calls) are NOT covered — see the
/// final report's "could not be tested" section.
/// </summary>
public class PaymentServiceTests
{
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IAuditService> _auditService = new();

    private PaymentService CreateSut(Data.ApplicationDbContext db, StripeSettings? settings = null) =>
        new(db, Options.Create(settings ?? new StripeSettings()), _emailService.Object, _auditService.Object);

    private static (Order order, Product product, Inventory inventory) SeedOrder(
        Data.ApplicationDbContext db,
        Guid userId,
        decimal totalAmount = 50m,
        OrderStatus status = OrderStatus.Pending,
        int quantityAvailable = 10,
        int quantityReserved = 0)
    {
        // Order.UserId is a required FK to Users; SQLite enforces it (unlike the InMemory
        // provider), so a matching User row must exist before an Order referencing it can be saved.
        if (!db.Users.Any(u => u.UserId == userId))
        {
            db.Users.Add(new User
            {
                UserId = userId,
                Email = $"seeded_{userId:N}@test.local",
                PasswordHash = "hash",
                FirstName = "Seeded",
                LastName = "User",
                Address = "1 Test St",
                City = "Testville",
                PostalCode = "00000",
                Country = "USA",
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var category = new Category { Name = "Cat" };
        db.Categories.Add(category);
        db.SaveChanges();

        var product = new Product { CategoryId = category.CategoryId, Name = "Widget", Price = 10m };
        db.Products.Add(product);
        db.SaveChanges();

        db.Inventories.Add(new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = quantityAvailable,
            QuantityReserved = quantityReserved
        });
        db.SaveChanges();

        var order = new Order
        {
            UserId = userId,
            OrderStatus = status,
            TotalAmount = totalAmount,
            CreatedDate = DateTime.UtcNow
        };
        db.Orders.Add(order);
        db.SaveChanges();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.OrderId,
            ProductId = product.ProductId,
            Quantity = 1,
            UnitPrice = product.Price,
            DeliveryDays = 2
        });
        db.SaveChanges();

        return (order, product, db.Inventories.Single(i => i.ProductId == product.ProductId));
    }

    // ---- CreatePaymentIntentAsync guard clauses ----

    [Fact]
    public async Task CreatePaymentIntentAsync_ReturnsNull_WhenOrderNotFound()
    {
        using var sqlite = new SqliteTestDatabase();
        var sut = CreateSut(sqlite.Context);

        var result = await sut.CreatePaymentIntentAsync(Guid.NewGuid(), new CreatePaymentRequestDto { OrderId = 999 });

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_ReturnsNull_WhenOrderBelongsToAnotherUser()
    {
        using var sqlite = new SqliteTestDatabase();
        var (order, _, _) = SeedOrder(sqlite.Context, Guid.NewGuid());
        var sut = CreateSut(sqlite.Context);

        var result = await sut.CreatePaymentIntentAsync(Guid.NewGuid(), new CreatePaymentRequestDto { OrderId = order.OrderId });

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Throws_WhenTotalAmountNotPositive()
    {
        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        var (order, _, _) = SeedOrder(sqlite.Context, userId, totalAmount: 0m);
        var sut = CreateSut(sqlite.Context);

        var act = () => sut.CreatePaymentIntentAsync(userId, new CreatePaymentRequestDto { OrderId = order.OrderId });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Throws_WhenOrderCancelled()
    {
        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        var (order, _, _) = SeedOrder(sqlite.Context, userId, status: OrderStatus.Cancelled);
        var sut = CreateSut(sqlite.Context);

        var act = () => sut.CreatePaymentIntentAsync(userId, new CreatePaymentRequestDto { OrderId = order.OrderId });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cancelled orders*");
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Throws_WhenAlreadySucceeded()
    {
        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        var (order, _, _) = SeedOrder(sqlite.Context, userId);
        sqlite.Context.Payment.Add(new Payment
        {
            OrderId = order.OrderId,
            StripePaymentIntentId = "pi_existing",
            Amount = order.TotalAmount,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Succeeded,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        await sqlite.Context.SaveChangesAsync();

        var sut = CreateSut(sqlite.Context);
        var act = () => sut.CreatePaymentIntentAsync(userId, new CreatePaymentRequestDto { OrderId = order.OrderId });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been paid*");
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Throws_WhenStripeSecretKeyMissing()
    {
        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        var (order, _, _) = SeedOrder(sqlite.Context, userId);
        var sut = CreateSut(sqlite.Context, new StripeSettings { SecretKey = "" });

        var act = () => sut.CreatePaymentIntentAsync(userId, new CreatePaymentRequestDto { OrderId = order.OrderId });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");
    }

    // ---- ConfirmTestPaymentAsync guard clauses ----

    [Fact]
    public async Task ConfirmTestPaymentAsync_ReturnsNull_WhenPaymentNotFound()
    {
        using var sqlite = new SqliteTestDatabase();
        var sut = CreateSut(sqlite.Context);

        var result = await sut.ConfirmTestPaymentAsync(
            Guid.NewGuid(), new ConfirmTestPaymentRequestDto { PaymentId = 999 });

        result.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmTestPaymentAsync_ReturnsExistingDto_WhenAlreadySucceeded()
    {
        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        var (order, _, _) = SeedOrder(sqlite.Context, userId);
        var payment = new Payment
        {
            OrderId = order.OrderId,
            StripePaymentIntentId = "pi_done",
            Amount = order.TotalAmount,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Succeeded,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        sqlite.Context.Payment.Add(payment);
        await sqlite.Context.SaveChangesAsync();

        var sut = CreateSut(sqlite.Context);
        var result = await sut.ConfirmTestPaymentAsync(
            userId, new ConfirmTestPaymentRequestDto { PaymentId = payment.PaymentId });

        result!.PaymentStatus.Should().Be(PaymentStatus.Succeeded.ToString());
        _emailService.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmTestPaymentAsync_Throws_WhenOrderCancelled()
    {
        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        var (order, _, _) = SeedOrder(sqlite.Context, userId, status: OrderStatus.Cancelled);
        var payment = new Payment
        {
            OrderId = order.OrderId,
            StripePaymentIntentId = "pi_cancelled_order",
            Amount = order.TotalAmount,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        sqlite.Context.Payment.Add(payment);
        await sqlite.Context.SaveChangesAsync();

        var sut = CreateSut(sqlite.Context);
        var act = () => sut.ConfirmTestPaymentAsync(userId, new ConfirmTestPaymentRequestDto { PaymentId = payment.PaymentId });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cancelled orders*");
    }

    [Fact]
    public async Task ConfirmTestPaymentAsync_Throws_WhenStripeSecretKeyMissing()
    {
        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        var (order, _, _) = SeedOrder(sqlite.Context, userId);
        var payment = new Payment
        {
            OrderId = order.OrderId,
            StripePaymentIntentId = "pi_no_key",
            Amount = order.TotalAmount,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        sqlite.Context.Payment.Add(payment);
        await sqlite.Context.SaveChangesAsync();

        var sut = CreateSut(sqlite.Context, new StripeSettings { SecretKey = "" });
        var act = () => sut.ConfirmTestPaymentAsync(userId, new ConfirmTestPaymentRequestDto { PaymentId = payment.PaymentId });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");
    }

    // ---- HandleWebhookAsync guard clauses (no network calls; EventUtility.ConstructEvent is local) ----

    [Fact]
    public async Task HandleWebhookAsync_Throws_WhenWebhookSecretNotConfigured()
    {
        using var sqlite = new SqliteTestDatabase();
        var sut = CreateSut(sqlite.Context, new StripeSettings { WebhookSecret = "" });

        var act = () => sut.HandleWebhookAsync("{}", "t=1,v1=abc");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*webhook secret*");
    }

    [Fact]
    public async Task HandleWebhookAsync_Throws_WhenSignatureMissing()
    {
        using var sqlite = new SqliteTestDatabase();
        var sut = CreateSut(sqlite.Context, new StripeSettings { WebhookSecret = "whsec_test" });

        var act = () => sut.HandleWebhookAsync("{}", "");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*signature is missing*");
    }

    [Fact]
    public async Task HandleWebhookAsync_Throws_WhenSignatureInvalid()
    {
        using var sqlite = new SqliteTestDatabase();
        var sut = CreateSut(sqlite.Context, new StripeSettings { WebhookSecret = "whsec_test" });

        var act = () => sut.HandleWebhookAsync("{\"type\":\"payment_intent.succeeded\"}", "t=1,v1=not-a-real-signature");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Invalid Stripe webhook signature*");
    }

    private static string BuildWebhookPayload(string paymentIntentId) =>
        $$"""
        {
          "id": "evt_test_webhook",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "created": 1700000000,
          "livemode": false,
          "pending_webhooks": 1,
          "type": "payment_intent.succeeded",
          "request": { "id": null, "idempotency_key": null },
          "data": {
            "object": {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "amount": 1000,
              "currency": "usd",
              "status": "succeeded",
              "livemode": false
            }
          }
        }
        """;

    private static string BuildSignatureHeader(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={timestamp},v1={signature}";
    }

    [Fact]
    public async Task HandleWebhookAsync_IsNoOp_WhenEventTypeIsNotPaymentIntentSucceeded()
    {
        const string secret = "whsec_test_secret";
        // api_version must be present and match the installed Stripe.net SDK's compiled
        // version (Stripe.StripeConfiguration.ApiVersion): Stripe.EventUtility.ConstructEvent's
        // own IsCompatibleApiVersion check throws a NullReferenceException (a Stripe.net SDK
        // bug, not Retalon code) when api_version is missing from the event payload, and throws
        // a version-mismatch StripeException when it doesn't match - confirmed empirically.
        var payload = $"{{\"id\":\"evt_1\",\"object\":\"event\",\"api_version\":\"{Stripe.StripeConfiguration.ApiVersion}\",\"type\":\"payment_intent.created\",\"data\":{{\"object\":{{}}}}}}";
        var signature = BuildSignatureHeader(payload, secret);

        using var sqlite = new SqliteTestDatabase();
        var sut = CreateSut(sqlite.Context, new StripeSettings { WebhookSecret = secret });

        await sut.HandleWebhookAsync(payload, signature);

        sqlite.Context.Payment.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleWebhookAsync_IsNoOp_WhenPaymentNotFoundForIntent()
    {
        const string secret = "whsec_test_secret";
        var payload = BuildWebhookPayload("pi_does_not_exist_locally");
        var signature = BuildSignatureHeader(payload, secret);

        using var sqlite = new SqliteTestDatabase();
        var sut = CreateSut(sqlite.Context, new StripeSettings { WebhookSecret = secret });

        await sut.HandleWebhookAsync(payload, signature);

        _emailService.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Empirically confirms the suspected production bug: HandleWebhookAsync's query
    /// (_context.Payment.Include(p => p.Order)) never chains .ThenInclude(o => o.User),
    /// yet the success path later dereferences payment.Order.User.Email. With a real
    /// pending payment on a non-cancelled order and sufficient inventory, this throws
    /// NullReferenceException instead of sending the confirmation email. Per instructions,
    /// this is documented only — production code is NOT modified to fix it.
    /// </summary>
    [Fact]
    public async Task HandleWebhookAsync_ThrowsNullReferenceException_BecauseOrderUserIsNotIncluded()
    {
        const string secret = "whsec_test_secret";
        const string paymentIntentId = "pi_confirmed_success";

        using var sqlite = new SqliteTestDatabase();
        var userId = Guid.NewGuid();
        sqlite.Context.Users.Add(new User
        {
            UserId = userId,
            Email = "buyer@test.local",
            PasswordHash = "hash",
            FirstName = "Buyer",
            LastName = "User",
            Address = "1 Test St",
            City = "Testville",
            PostalCode = "00000",
            Country = "USA",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        });
        await sqlite.Context.SaveChangesAsync();

        var (order, _, _) = SeedOrder(sqlite.Context, userId, quantityAvailable: 10, quantityReserved: 0);

        sqlite.Context.Payment.Add(new Payment
        {
            OrderId = order.OrderId,
            StripePaymentIntentId = paymentIntentId,
            Amount = order.TotalAmount,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        await sqlite.Context.SaveChangesAsync();

        var payload = BuildWebhookPayload(paymentIntentId);
        var signature = BuildSignatureHeader(payload, secret);

        // Detach the User/Order/Payment entities seeded above via this same DbContext
        // instance: without this, EF Core's identity-map navigation fixup would silently
        // populate payment.Order.User from the already-tracked User entity, masking the
        // missing .ThenInclude(o => o.User) bug. A real request uses a fresh, per-scope
        // DbContext that never tracked the User, so this Clear() reproduces that condition.
        sqlite.Context.ChangeTracker.Clear();

        var sut = CreateSut(sqlite.Context, new StripeSettings { WebhookSecret = secret });

        var act = () => sut.HandleWebhookAsync(payload, signature);

        await act.Should().ThrowAsync<NullReferenceException>();
    }
}
