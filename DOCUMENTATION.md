# Retalon — Full System Documentation

> A complete reference for the Retalon retail/e-commerce backend: what every module does, what goes in and out of it, how data flows end-to-end, and the exact SQL you can run in **SSMS** against the `RetalonDb` database to verify any of it.
>
> **How to use this file:** it is organized so you can `Ctrl+F` for a keyword (a table name, a controller name, an endpoint route, an enum value) and land in the right section. Every module follows the same shape: **Purpose → Database Tables → API Endpoints (Input/Output) → Step-by-Step Flow → SSMS Verification Queries → Notes/Gotchas**.

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Tech Stack & Cross-Cutting Concerns](#2-tech-stack--cross-cutting-concerns)
3. [Database Schema Reference (all tables)](#3-database-schema-reference-all-tables)
4. [SSMS Quick-Start & General-Purpose Queries](#4-ssms-quick-start--general-purpose-queries)
5. [Module: Authentication & Users](#5-module-authentication--users-authcontroller)
6. [Module: Cart](#6-module-cart-cartcontroller)
7. [Module: Category](#7-module-category-categorycontroller)
8. [Module: Product Catalog & Search](#8-module-product-catalog--search-productcontroller)
9. [Module: Vendor Bulk Product Import (Excel)](#9-module-vendor-bulk-product-import-excel)
10. [Module: Inventory](#10-module-inventory-inventorycontroller)
11. [Module: Orders](#11-module-orders-ordercontroller)
12. [Module: Payments (Stripe)](#12-module-payments-stripe-paymentcontroller--stripewebhookcontroller)
13. [Module: Procurement](#13-module-procurement-procurementcontroller)
14. [Module: Notifications & Email](#14-module-notifications--email-notificationscontroller)
15. [Module: Audit Logging](#15-module-audit-logging-auditservice)
16. [Module: Security Event Logging](#16-module-security-event-logging-securityeventservice)
17. [End-to-End Flow: "Add to cart → Checkout → Pay → Fulfill"](#17-end-to-end-flow-add-to-cart--checkout--pay--fulfill)
18. [Enums Reference](#18-enums-reference)
19. [Known Gaps / Behavior Notes (read before debugging)](#19-known-gaps--behavior-notes-read-before-debugging)

---

## 1. System Overview

**Retalon** is an ASP.NET Core Web API (.NET, `net10.0`) retail backend. It exposes a JWT-secured REST API for:

- Registering/authenticating users (with account lockout and refresh tokens)
- Browsing/searching a product catalog that is backed by a local SQL Server database **and** falls back to the public **Open Food Facts** API when a product isn't found locally (auto-imports it)
- Bulk-importing vendor products from an Excel (`.xlsx`) sheet
- Managing a shopping cart
- Creating orders from the cart (with inventory checks + delivery-date estimation)
- Taking payment via **Stripe** (PaymentIntents + webhook confirmation)
- Raising **procurement** requests when an order can't be fully covered by stock
- Sending emails (order confirmations, test emails) via SMTP (MailKit)
- Auditing user actions and logging security events (failed logins, lockouts, token revocation)
- Running background jobs via **Hangfire**

Database name: **`RetalonDb`** (SQL Server / LocalDB by default — see `appsettings.json`, key `ConnectionStrings:DefaultConnection`).

---

## 2. Tech Stack & Cross-Cutting Concerns

| Concern | Detail |
|---|---|
| Framework | ASP.NET Core (.NET 10), Controllers (`[ApiController]`) |
| ORM | Entity Framework Core, SQL Server provider, Fluent API configs in `Retalon/Data/Configurations/*.cs` |
| Auth | JWT Bearer tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`). **Global fallback policy requires authentication on every endpoint** unless marked `[AllowAnonymous]` |
| Password hashing | BCrypt.Net (`BCrypt.Net.BCrypt.HashPassword` / `.Verify`) |
| Refresh tokens | Random 64-byte token, stored **hashed with BCrypt** in `RefreshTokens.TokenHash` (never stored in plaintext) |
| Rate limiting | ASP.NET Core `RateLimiter` middleware. Policies: `LoginPolicy` (10 req/min, fixed window), `SearchPolicy` (30 req/min, fixed window). **`PaymentPolicy` is referenced by `PaymentController` but never defined in `Program.cs`** — see [Known Gaps](#19-known-gaps--behavior-notes-read-before-debugging) |
| API versioning | `Asp.Versioning`, default version 1.0, assumed when unspecified |
| Caching | `IMemoryCache`, used in `ProductService` for search/detail lookups (5 minute sliding TTL per cache key) |
| Background jobs | Hangfire (SQL Server storage), one recurring job `retalon-test-job` running every minute, calling `RetalonBackgroundJobs.TestJob()` (just logs) |
| Logging | Serilog → Console + rolling file `Logs/log-.txt` (daily, 30 days retained) |
| Exception handling | `GlobalExceptionHandler` (`IExceptionHandler`) maps exceptions to HTTP codes: `UnauthorizedAccessException`→401, `KeyNotFoundException`→404, `ArgumentException`/`InvalidOperationException`→400, everything else→500. Returns RFC7807 `ProblemDetails` JSON |
| Security headers | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy: camera=(), microphone=(), geolocation=()` added to every response |
| CORS | Policy `FrontendPolicy` allows `localhost:3000` and `localhost:4200` (http/https), any header/method |
| Health checks | `GET /health` (always healthy), `GET /health/database` (runs `db.Database.CanConnectAsync()`) — both anonymous |
| API docs | OpenAPI + Scalar UI, **development environment only**, anonymous |
| External integration | Open Food Facts public API (`world.openfoodfacts.org`) for product enrichment |

---

## 3. Database Schema Reference (all tables)

Table names below are the **exact** names EF Core created (from the migrations) — note the two irregular ones: `Payment` (singular) and `auditLogs` (lowercase).

| Table | Primary Key | Notable Columns | Key Relationships |
|---|---|---|---|
| `Users` | `UserId` (uniqueidentifier) | `Email` (unique), `PasswordHash`, `IsActive`, `FailedLoginAttempts`, `LockedUntil`, `CreatedDate`, `LastLoginDate` | 1→N `UserRoles`, `RefreshTokens`, `Carts`(1:1), `Orders`, `SecurityEvents`, `Notifications`, `auditLogs`, `SearchHistories` |
| `Roles` | `RoleId` (uniqueidentifier) | `Name` (unique) — seeded: `Admin`, `Customer`, `WarehouseManager` | 1→N `UserRoles` |
| `UserRoles` | composite (`UserId`, `RoleId`) | — | N:N join between `Users` and `Roles` |
| `RefreshTokens` | `RefreshTokenId` | `UserId`, `TokenHash` (unique), `ExpiryDate`, `RevokedDate`, `CreatedByIp` | FK → `Users.UserId` (cascade delete) |
| `SecurityEvents` | `SecurityEventId` (bigint) | `UserId` (nullable), `SecurityEventType`, `Description`, `IpAddress`, `CreatedDate` | FK → `Users.UserId` (SET NULL on delete) |
| `auditLogs` | `AuditLogId` (bigint) | `UserId` (nullable), `PerformedByUserId` (no FK), `Action`, `EntityName`, `OldValue`, `NewValue`, `Timestamp` | FK `UserId` → `Users.UserId` (SET NULL) |
| `Categories` | `CategoryId` (bigint) | `Name` (unique), `Description` | 1→N `Products` |
| `Products` | `ProductId` (bigint) | `CategoryId`, `ExternalProductId`, `Name`, `Barcode`, `Price` (decimal 18,2), `Currency`, `ImportSource`, `ProductStatus` (string), `IsDeleted`, `CreatedDate`, `LastUpdated`, `DeletedDate` | FK → `Categories`; 1:1 → `Inventories` (cascade) |
| `Inventories` | `InventoryId` (bigint) | `ProductId` (unique), `QuantityAvailable`, `QuantityReserved`, `SafetyStockLevel`, `ProcurementLeadTimeDays`, `LastUpdated` | FK → `Products.ProductId` (1:1, cascade) |
| `Carts` | `CartId` (uniqueidentifier) | `UserId` | FK → `Users.UserId` (1:1, cascade) |
| `CartItems` | `CartItemId` (bigint) | `CartId`, `ProductId`, `Quantity`, `AddedDate`. **Unique index on (`CartId`,`ProductId`)** | FK → `Carts` (cascade), `Products` (restrict) |
| `Orders` | `OrderId` (bigint) | `UserId`, `OrderStatus` (string), `TotalAmount` (decimal 18,2), `ExpectedDeliveryDate`, `CreatedDate`, `UpdatedDate` | FK → `Users` (restrict); 1→N `OrderItems` (cascade), `Payment` (cascade), `Procurements` (restrict) |
| `OrderItems` | `OrderItemId` (bigint) | `OrderId`, `ProductId`, `Quantity`, `UnitPrice` (decimal 18,2), `DeliveryDays` | FK → `Orders`, `Products` (restrict) |
| `Payment` | `PaymentId` (bigint) | `OrderId`, `StripePaymentIntentId` (unique), `Amount` (decimal 18,2), `Currency`, `PaymentStatus` (string), `FailureReason`, `CreatedDate`, `UpdatedDate` | FK → `Orders.OrderId` |
| `Procurements` | `ProcurementId` (bigint) | `OrderId`, `ProductId`, `RequiredQuantity`, `ProcurementStatus` (string), `ExpectedArrivalDate`, `CreatedDate`, `UpdatedDate` | FK → `Orders` (restrict), `Products` (restrict) |
| `Notifications` | `NotificationId` (bigint) | `UserId`, `NotificationType` (string), `Message`, `NotificationStatus` (string), `SentDate`, `CreatedDate` | FK → `Users` (cascade). **Currently no service writes to this table** — see notes |
| `SearchHistories` | `SearchHistoryId` (bigint) | `UserId`, `SearchTerm`, `FoundLocally`, `SearchDate` | FK → `Users` (cascade). **Currently no service writes to this table** — see notes |
| `ImportBatches` | `ImportBatchId` (bigint) | `UserId`, `FileName`, `Status` (string), `TotalRows`, `InsertedRows`, `UpdatedRows`, `FailedRows`, `ErrorSummary`, `StartedDate`, `CompletedDate` | FK → `Users` (restrict) |

**Enum-backed columns are stored as `nvarchar` strings** (e.g. `ProductStatus`, `OrderStatus`, `PaymentStatus`, `ProcurementStatus`, `NotificationType`, `NotificationStatus`, `SecurityEventType`, `ImportBatchStatus`) — EF Core is configured with `.HasConversion<string>()` for all of them, so in SSMS you filter with `WHERE OrderStatus = 'Pending'`, not with an integer.

---

## 4. SSMS Quick-Start & General-Purpose Queries

Connect SSMS to the server from `appsettings.json` (`(localdb)\MSSQLLocalDB` by default), then:

```sql
USE RetalonDb;
GO
```

### 4.1 List all tables and row counts
```sql
SELECT
    t.name AS TableName,
    p.rows AS RowCount
FROM sys.tables t
JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
ORDER BY t.name;
```

### 4.2 List all columns for a given table (swap the table name)
```sql
SELECT
    COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Products'
ORDER BY ORDINAL_POSITION;
```

### 4.3 List all foreign keys in the database
```sql
SELECT
    fk.name AS ForeignKey,
    tp.name AS ParentTable,
    cp.name AS ParentColumn,
    tr.name AS ReferencedTable,
    cr.name AS ReferencedColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
ORDER BY tp.name;
```

### 4.4 List all unique indexes (great for verifying "Name is unique", "one cart per user", etc.)
```sql
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    i.is_unique,
    STRING_AGG(c.name, ', ') AS Columns
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.is_unique = 1
GROUP BY t.name, i.name, i.is_unique
ORDER BY t.name;
```

### 4.5 Check applied EF Core migrations
```sql
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
```
Expect 3 rows: `InitialCreate`, `SeedRoles`, `AddImportBatch`.

### 4.6 Wipe test data quickly (dev database only!)
```sql
-- Order matters because of FK constraints — delete children first.
DELETE FROM Procurements;
DELETE FROM Payment;
DELETE FROM OrderItems;
DELETE FROM Orders;
DELETE FROM CartItems;
DELETE FROM Carts WHERE UserId NOT IN (SELECT UserId FROM Users); -- optional cleanup
DELETE FROM Inventories;
DELETE FROM Products;
DELETE FROM Categories WHERE Name <> 'Imported';
```

---

## 5. Module: Authentication & Users (`AuthController`)

**Files:** `Controllers/AuthController.cs`, `Services/AuthService.cs`, `Services/TokenService.cs`, `DTOs/Auth/*`
**Tables touched:** `Users`, `Roles`, `UserRoles`, `RefreshTokens`, `Carts` (a cart is auto-created on register), `SecurityEvents`, `auditLogs`

### 5.1 Purpose
Handles registration, login (with lockout protection), JWT access-token issuance, refresh-token rotation, and logout (refresh-token revocation).

### 5.2 API Endpoints

| Method & Route | Auth | Input (Body) | Output |
|---|---|---|---|
| `POST /api/Auth/register` | Anonymous | `RegisterRequestDto`: `FirstName`, `LastName`, `Email`, `Password` (min 8 chars), `PhoneNumber`, `AddressLine1`, `AddressLine2?`, `City`, `State`, `PostalCode`, `Country` | `200 OK { message: "User registered successfully." }` or `400` if email exists / no Customer role configured |
| `POST /api/Auth/login` | Anonymous, rate-limited (`LoginPolicy`: 10/min) | `LoginRequestDto`: `Email`, `Password` | `200 OK` → `AuthResponseDto` (`AccessToken`, `RefreshToken`, `AccessTokenExpiresAt`) or `401` |
| `POST /api/Auth/refresh` | Anonymous | `RefreshTokenRequestDto`: `RefreshToken` | `200 OK` → new `AuthResponseDto` or `401` |
| `POST /api/Auth/logout` | Anonymous | `LogoutRequestDto`: `RefreshToken` | `200 OK { message: "Logged out successfully." }` or `401` |

### 5.3 Flow — Register
1. Email normalized to lowercase/trimmed; reject if a `Users` row with that email already exists.
2. Look up the `Customer` role by name; fail (400) if it's missing — this is why `Roles` must be seeded before anyone can register.
3. Create `User` row: `PasswordHash` = `BCrypt.HashPassword(Password)`. **Note:** `AddressLine2`, `State`, `PhoneNumber` from the DTO are accepted but **never persisted** — only `Address` (=AddressLine1), `City`, `PostalCode`, `Country` are stored.
4. Insert `UserRole` (User ↔ Customer role).
5. Insert a `Cart` row for the new user (empty cart, ready to use).
6. Single `SaveChangesAsync()` commits all three inserts together.

### 5.4 Flow — Login
1. Look up user by (lowercased) email, including `UserRoles.Role`.
2. Reject (401) if user not found, `IsActive = false`, or `LockedUntil` is in the future.
3. Verify password with BCrypt.
4. **On failure:** increment `FailedLoginAttempts`. If it reaches **5**, set `LockedUntil = now + 15 minutes`, reset the counter to 0, and log a `SecurityEvent` of type `AccountLocked`. Otherwise log `FailedLogin`. Either way, throws `401`.
5. **On success:** reset `FailedLoginAttempts` to 0, clear `LockedUntil`, stamp `LastLoginDate`. Write an `AuditLog` entry (`Action = "UserLogin"`).
6. Generate a JWT access token (claims: `sub`, `email`, `NameIdentifier`, `Email`, one `role` claim per assigned role) valid for `Jwt:AccessTokenExpirationMinutes` (default 15 min).
7. Generate a random refresh token (64 random bytes, Base64), **hash it with BCrypt**, store in `RefreshTokens` with a 7-day expiry.
8. Return both tokens + the access token's expiry timestamp.

### 5.5 Flow — Refresh
1. Load **all non-revoked, non-expired** `RefreshTokens` (`RevokedDate IS NULL AND ExpiryDate > now`) — because tokens are hashed, there is **no way to filter by value in SQL**; the code loads all valid tokens into memory and BCrypt-verifies each one until it finds a match. (Fine at small scale, but note this is an O(n) scan over live refresh tokens.)
2. If no match → 401. If the owning user is inactive → 401.
3. Revoke the old token (`RevokedDate = now`) and issue a brand-new access + refresh token pair (rotation). The new refresh token is stored as a new row; the old row is kept (soft-revoked) for audit purposes.

### 5.6 Flow — Logout
1. Same BCrypt-scan approach over non-revoked tokens to find the matching one.
2. Sets `RevokedDate = now`, logs a `SecurityEvent` of type `TokenRevoked`.
3. If nothing matches → 401.

### 5.7 SSMS Verification Queries

```sql
-- All users with their roles
SELECT u.UserId, u.Email, u.IsActive, u.FailedLoginAttempts, u.LockedUntil,
       u.CreatedDate, u.LastLoginDate, r.Name AS Role
FROM Users u
JOIN UserRoles ur ON ur.UserId = u.UserId
JOIN Roles r ON r.RoleId = ur.RoleId
ORDER BY u.CreatedDate DESC;

-- Currently locked-out accounts
SELECT Email, FailedLoginAttempts, LockedUntil
FROM Users
WHERE LockedUntil IS NOT NULL AND LockedUntil > SYSUTCDATETIME();

-- Active (non-revoked, non-expired) refresh tokens per user
SELECT u.Email, rt.RefreshTokenId, rt.CreatedDate, rt.ExpiryDate, rt.RevokedDate, rt.CreatedByIp
FROM RefreshTokens rt
JOIN Users u ON u.UserId = rt.UserId
WHERE rt.RevokedDate IS NULL AND rt.ExpiryDate > SYSUTCDATETIME()
ORDER BY rt.CreatedDate DESC;

-- Confirm the 3 seeded roles exist
SELECT * FROM Roles;

-- Verify a specific user has the Customer role and an empty cart was created
SELECT u.Email, r.Name AS Role, c.CartId
FROM Users u
JOIN UserRoles ur ON ur.UserId = u.UserId
JOIN Roles r ON r.RoleId = ur.RoleId
LEFT JOIN Carts c ON c.UserId = u.UserId
WHERE u.Email = 'someone@example.com';

-- Recent security events (lockouts, failed logins, token revocations)
SELECT TOP 50 se.CreatedDate, se.SecurityEventType, se.Description, u.Email, se.IpAddress
FROM SecurityEvents se
LEFT JOIN Users u ON u.UserId = se.UserId
ORDER BY se.CreatedDate DESC;
```

### 5.8 Notes
- `Jwt:Key` **must** be set in configuration or the app throws on startup when the auth middleware tries to validate a token.
- Passwords and refresh tokens are one-way hashed — you can never "look up" a refresh token by value in SQL; you can only inspect metadata (dates, revocation) or delete/revoke rows directly.

---

## 6. Module: Cart (`CartController`)

**Files:** `Controllers/CartController.cs`, `Services/CartService.cs`, `DTOs/Cart/*`
**Tables touched:** `Carts`, `CartItems`, `Products` (read), `Inventories` (read)

### 6.1 Purpose
Every user has exactly one cart (created at registration, or lazily on first `GET`/`POST`). This module lets the authenticated user view their cart, add items (respecting available stock), and remove items.

### 6.2 API Endpoints — all require `[Authorize]` (any authenticated user); user identity comes from the JWT's `NameIdentifier` claim.

| Method & Route | Input | Output |
|---|---|---|
| `GET /api/cart` | — | `200 OK` → `CartResponseDto` (`CartId`, `Items[]`, computed `Total`) |
| `POST /api/cart/items` | `AddCartItemRequestDto`: `ProductId` (1–1000), `Quantity` (1–100) | `200 OK` → updated `CartResponseDto` · `404` if product missing/inactive · `400` if quantity ≤ 0 or insufficient inventory |
| `DELETE /api/cart/items/{cartItemId}` | route param `cartItemId` | `204 No Content` on success · `404` if not found / not owned by caller |

`CartItemResponseDto` fields: `CartItemId`, `ProductId`, `ProductName`, `ImageUrl`, `Price`, `Quantity`, computed `Subtotal = Price * Quantity`.
`CartResponseDto` fields: `CartId`, `Items[]`, computed `Total = Σ Subtotal`.

### 6.3 Flow — Get Cart
1. Find the cart for `UserId` (from JWT), including items + product details.
2. If none exists yet, create an empty one on the spot and save it.
3. Map to DTO (totals computed client-side in the DTO's `get`-only properties, not stored in the DB).

### 6.4 Flow — Add Item
1. Validate `Quantity > 0` (throws `ArgumentException` → 400 otherwise).
2. Load the product; it must exist, not be soft-deleted (`IsDeleted = false`), and be `ProductStatus = Active` — otherwise returns `null` → controller returns `404`.
3. Load the product's `Inventory` row; if missing → `400` ("Inventory is not available for this product").
4. Compute `availableQuantity = QuantityAvailable - QuantityReserved`.
5. Load/create the cart. If the product is already in the cart, **add** the new quantity to the existing line's quantity (not replace).
6. If the combined quantity exceeds `availableQuantity` → `400` with the exact number still available.
7. Otherwise upsert the `CartItem` row (`AddedDate = UtcNow` for new rows) and save.

### 6.5 Flow — Remove Item
1. Find the `CartItem` by id **and** verify it belongs to a cart owned by the caller (`ci.Cart.UserId == userId`) — prevents deleting someone else's cart line by guessing an id.
2. Delete it. Returns `false` (→404) if not found/owned.

### 6.6 SSMS Verification Queries

```sql
-- Full contents of one user's cart with subtotal computed in SQL
SELECT u.Email, ci.CartItemId, p.Name AS ProductName, ci.Quantity, p.Price,
       (p.Price * ci.Quantity) AS Subtotal, ci.AddedDate
FROM Carts c
JOIN Users u ON u.UserId = c.UserId
JOIN CartItems ci ON ci.CartId = c.CartId
JOIN Products p ON p.ProductId = ci.ProductId
WHERE u.Email = 'someone@example.com';

-- Grand total per cart (all users)
SELECT c.CartId, u.Email, SUM(p.Price * ci.Quantity) AS CartTotal
FROM Carts c
JOIN Users u ON u.UserId = c.UserId
LEFT JOIN CartItems ci ON ci.CartId = c.CartId
LEFT JOIN Products p ON p.ProductId = ci.ProductId
GROUP BY c.CartId, u.Email
ORDER BY CartTotal DESC;

-- Verify every user has exactly one cart (1:1 relationship)
SELECT UserId, COUNT(*) AS CartCount
FROM Carts
GROUP BY UserId
HAVING COUNT(*) > 1;   -- should return 0 rows

-- Verify the "no duplicate product in same cart" unique index is holding
SELECT CartId, ProductId, COUNT(*) AS Occurrences
FROM CartItems
GROUP BY CartId, ProductId
HAVING COUNT(*) > 1;   -- should return 0 rows

-- Abandoned carts (items added but never converted to an order) older than 7 days
SELECT u.Email, ci.AddedDate, p.Name, ci.Quantity
FROM CartItems ci
JOIN Carts c ON c.CartId = ci.CartId
JOIN Users u ON u.UserId = c.UserId
JOIN Products p ON p.ProductId = ci.ProductId
WHERE ci.AddedDate < DATEADD(DAY, -7, SYSUTCDATETIME());
```

### 6.7 Notes
- Carts are **never deleted**; "checkout" (see Orders module) just removes the `CartItems`, the empty `Cart` row remains for reuse.
- `AddCartItemRequestDto.ProductId` has a `[Range(1,1000)]` validation attribute — if your real `ProductId`s exceed 1000, requests will fail model validation (400) before even reaching the service. Worth knowing if you seed a lot of test products.

---

## 7. Module: Category (`CategoryController`)

**Files:** `Controllers/CategoryController.cs` (talks to `ApplicationDbContext` directly — no dedicated service)
**Tables touched:** `Categories`

### 7.1 Purpose
Simple read-only lookup of all product categories, sorted alphabetically. Categories are otherwise created implicitly by the Product/Import modules (e.g., an "Imported" category is auto-created the first time an Open Food Facts product is pulled in).

### 7.2 API Endpoint

| Method & Route | Auth | Input | Output |
|---|---|---|---|
| `GET /api/categories` | Inherited global default = **authenticated** (no `[AllowAnonymous]` on this controller) | — | `200 OK` → array of `{ categoryId, name, description }`, ordered by `Name` |

### 7.3 SSMS Verification Queries

```sql
SELECT CategoryId, Name, Description FROM Categories ORDER BY Name;

-- Product count per category
SELECT c.Name, COUNT(p.ProductId) AS ProductCount
FROM Categories c
LEFT JOIN Products p ON p.CategoryId = c.CategoryId AND p.IsDeleted = 0
GROUP BY c.Name
ORDER BY ProductCount DESC;

-- Confirm category names are unique
SELECT Name, COUNT(*) FROM Categories GROUP BY Name HAVING COUNT(*) > 1;
```

---

## 8. Module: Product Catalog & Search (`ProductController`)

**Files:** `Controllers/ProductController.cs`, `Services/ProductService.cs`, `Services/OpenFoodFactsService.cs`, `DTOs/Products/*`, `DTOs/Common/PagedResponseDto.cs`
**Tables touched:** `Products`, `Categories`, `Inventories` (created for auto-imported products)
**External dependency:** `https://world.openfoodfacts.org` (public food-product database API)

### 8.1 Purpose
Search/browse the catalog. If nothing matches locally, the API silently reaches out to Open Food Facts, imports any matching products into the local DB (category `"Imported"`, `ImportSource = "OpenFoodFacts"`, `Price = 0` because the external API has no selling price), and returns those.

### 8.2 API Endpoints

| Method & Route | Auth | Input | Output |
|---|---|---|---|
| `GET /api/products/search` | Anonymous, rate-limited (`SearchPolicy`: 30/min) | Query: `query` (required), `page` (default 1), `pageSize` (default 20, max 100), `sortBy` (`price`\|`name`\|`createddate`, default name), `descending` (bool) | `200 OK` → `PagedResponseDto<ProductResponseDto>` · `400` if query blank/pageSize invalid |
| `GET /api/products/{productId}` | Anonymous | route `productId` (long) | `200 OK` → `ProductResponseDto` · `404` |
| `GET /api/products/barcode/{barcode}` | Anonymous | route `barcode` (string) | `200 OK` → `ProductResponseDto` (local, or fetched live from Open Food Facts if not local — **not persisted** in this path) · `404` |
| `POST /api/products/import/{barcode}` | Anonymous | route `barcode` | `200 OK` → newly **persisted** `ProductResponseDto` (creates `Product` + no `Inventory` row here) · `404` if not found locally or on Open Food Facts |

`ProductResponseDto`: `ProductId`, `CategoryId`, `ExternalProductId`, `Name`, `Barcode`, `Description`, `ImageUrl`, `Price`, `Currency`, `ImportSource`, `ProductStatus` (string).

`PagedResponseDto<T>`: `Items[]`, `Page`, `PageSize`, `TotalCount`, `TotalPages`.

### 8.3 Flow — Search
1. Validate/clamp `page` (≥1) and `pageSize` (1–100).
2. Cache key = `product-search:{term}:{page}:{pageSize}:{sortBy}:{descending}` — 5 minute in-memory cache; a cache hit returns instantly without touching the DB.
3. Query `Products` where `!IsDeleted AND ProductStatus != Inactive AND (Name CONTAINS term OR Barcode CONTAINS term)`, sorted per `sortBy`.
4. **If `TotalCount > 0`**: page it, map to DTOs, cache, return.
5. **If `TotalCount == 0`**: call Open Food Facts' search endpoint. For each external hit with a barcode:
   - Skip if already present locally by barcode.
   - Otherwise find-or-create the `"Imported"` category, insert a new `Product` (Active, `IsDeleted=false`, `ImportSource="OpenFoodFacts"`), and insert a matching `Inventory` row with **all quantities = 0**.
   - Each insert is its own `SaveChangesAsync()` (not batched, not transactional across rows).
6. Sort/paginate the (now-local) results in memory and cache/return them.

### 8.4 Flow — Get by Id / Get by Barcode
- By id: local-only lookup, 5-minute cache, 404 if missing or soft-deleted.
- By barcode: checks local DB first; if absent, calls Open Food Facts **live** and returns the mapped result **without saving it** to the database (this is the one read path that does not persist).

### 8.5 Flow — Import by barcode (`POST /api/products/import/{barcode}`)
1. If a non-deleted product with that barcode already exists locally, just return it (no external call).
2. Otherwise call Open Food Facts; 404 if not found there either.
3. Find-or-create `"Imported"` category, insert the `Product` with `Price = 0m`, `Currency = "USD"`. **No `Inventory` row is created in this path** (unlike the search-fallback path) — you must set stock via the Inventory module afterward.

### 8.6 SSMS Verification Queries

```sql
-- All auto-imported (Open Food Facts) products
SELECT ProductId, Name, Barcode, Price, ImportSource, ProductStatus, CreatedDate
FROM Products
WHERE ImportSource = 'OpenFoodFacts'
ORDER BY CreatedDate DESC;

-- Imported products that still have Price = 0 (need manual pricing)
SELECT ProductId, Name, Barcode, CreatedDate
FROM Products
WHERE ImportSource = 'OpenFoodFacts' AND Price = 0;

-- Imported products missing an Inventory row (imported via /import/{barcode}, not via search-fallback)
SELECT p.ProductId, p.Name, p.Barcode
FROM Products p
LEFT JOIN Inventories i ON i.ProductId = p.ProductId
WHERE p.ImportSource = 'OpenFoodFacts' AND i.InventoryId IS NULL;

-- Active vs inactive vs discontinued product counts
SELECT ProductStatus, COUNT(*) AS Count
FROM Products
WHERE IsDeleted = 0
GROUP BY ProductStatus;

-- Search-like query directly in SQL (mirrors the app's LIKE search)
SELECT ProductId, Name, Barcode, Price, ProductStatus
FROM Products
WHERE IsDeleted = 0
  AND ProductStatus <> 'Inactive'
  AND (Name LIKE '%milk%' OR Barcode LIKE '%milk%')
ORDER BY Name;

-- Soft-deleted products (should be hidden from all customer-facing endpoints)
SELECT ProductId, Name, DeletedDate FROM Products WHERE IsDeleted = 1;
```

### 8.7 Notes
- The in-memory cache means **updates to a product won't be visible via `GET /api/products/{id}` for up to 5 minutes**, and search results likewise. If you edit data directly in SSMS for testing, expect stale API responses until the cache entry expires or the app restarts.
- `IMemoryCache` is per-process — in a multi-instance deployment each instance has its own cache; there is no shared/distributed cache invalidation.

---

## 9. Module: Vendor Bulk Product Import (Excel)

**Files:** `Controllers/ProductController.cs` (`POST /api/products/import/bulk`), `Services/VendorProductImportService.cs`, `DTOs/Products/VendorProductImportResultDto.cs`, `DTOs/Products/VendorProductImportRowDto.cs`, `Models/Entities/ImportBatch.cs`
**Tables touched:** `ImportBatches`, `Categories`, `Products`, `Inventories`
**Library:** ClosedXML (reads `.xlsx`)
**Sample template:** `Retalon/Retalon_Vendor_Product_Import_Template.xlsx` (in repo root of the API project)

### 9.1 Purpose
Lets an Admin/WarehouseManager upload an Excel sheet of products (with stock levels) to insert new products or update existing ones (matched by `ExternalProductId` first, then `Barcode`) in one transactional batch, tracked via an `ImportBatch` audit row.

### 9.2 API Endpoint

| Method & Route | Auth | Input | Output |
|---|---|---|---|
| `POST /api/products/import/bulk` | `[Authorize(Roles = "Admin,WarehouseManager")]`. Max upload size 10 MB | `multipart/form-data` with `file` = a `.xlsx` file | `200 OK` → `VendorProductImportResultDto` |

**Required Excel header row** (first sheet, first used row), column order matters for data (headers matched case-insensitively, values read positionally by column index 1–11):

| Col | Header | Required? |
|---|---|---|
| 1 | `ExternalProductId` | optional |
| 2 | `Name` | **required** |
| 3 | `Barcode` | optional |
| 4 | `Description` | optional |
| 5 | `ImageUrl` | optional |
| 6 | `Price` | **required**, ≥ 0 |
| 7 | `Currency` | optional (defaults `USD`) |
| 8 | `Category` | **required** |
| 9 | `QuantityAvailable` | optional, ≥ 0 |
| 10 | `SafetyStockLevel` | optional, ≥ 0 |
| 11 | `ProcurementLeadTimeDays` | optional, ≥ 0 |

**`VendorProductImportResultDto`** output: `Success` (bool), `ImportBatchId`, `TotalRows`, `Inserted`, `Updated`, `Failed`, `Status` (`"Completed"` or `"CompletedWithErrors"`), `Errors[]` (`{ RowNumber, Error }`).

### 9.3 Flow (all inside one DB transaction)
1. Validate the file is present and has a `.xlsx` extension.
2. Open workbook → first worksheet → validate the header row contains all 11 required headers (missing any → `400 ArgumentException`, nothing is saved).
3. Read every non-blank data row into a `VendorProductImportRowDto`.
4. **Per-row validation**: `Name` required, `Category` required, `Price` required and ≥ 0, and any of `QuantityAvailable`/`SafetyStockLevel`/`ProcurementLeadTimeDays` if present must be ≥ 0. Rows failing validation are collected into `Errors[]` and **excluded** from processing (they don't block the other rows).
5. Insert an `ImportBatch` row immediately (`Status = Processing`, `TotalRows`, `FailedRows` = validation failures so far) and save — this row exists even if everything after it fails.
6. **Category resolution**: collect distinct category names (case-insensitive) from valid rows; create any that don't already exist in `Categories`.
7. **Product matching**: for all valid rows, batch-look-up existing `Products` whose `ExternalProductId` or `Barcode` matches any row's value. Build two lookup dictionaries (by external id, by barcode).
8. **Upsert loop**: for each valid row — if a matching product is found (external id checked first, then barcode), **update** it (`CategoryId`, `Name`, `Barcode` [only if provided], `Description`, `ImageUrl`, `Price`, `Currency` [only if provided], `ImportSource = "VendorBulkImport"`, `IsDeleted = false`, `LastUpdated = now`); otherwise **insert** a new `Product` (`ImportSource = "VendorBulkImport"`, `ProductStatus = Active`).
9. **Inventory upsert loop**: for each valid row's resolved product, if an `Inventory` row exists, **overwrite** `QuantityAvailable`/`SafetyStockLevel`/`ProcurementLeadTimeDays` (`QuantityReserved` is left untouched); otherwise insert a new `Inventory` row with those values and `QuantityReserved = 0`.
10. Update the `ImportBatch` row: `InsertedRows`, `UpdatedRows`, `FailedRows`, `Status = Completed` (if zero errors) or `CompletedWithErrors`, `CompletedDate = now`.
11. **Commit the transaction.** If any unhandled exception occurs anywhere in steps 5–10, the **whole transaction rolls back** — including the `ImportBatch` insert itself (nothing is left half-done).

### 9.4 SSMS Verification Queries

```sql
-- Import batch history
SELECT ib.ImportBatchId, u.Email AS ImportedBy, ib.FileName, ib.Status,
       ib.TotalRows, ib.InsertedRows, ib.UpdatedRows, ib.FailedRows,
       ib.StartedDate, ib.CompletedDate, ib.ErrorSummary
FROM ImportBatches ib
JOIN Users u ON u.UserId = ib.UserId
ORDER BY ib.StartedDate DESC;

-- Products from the most recent vendor bulk import
SELECT TOP 100 ProductId, Name, Barcode, Price, CategoryId, LastUpdated
FROM Products
WHERE ImportSource = 'VendorBulkImport'
ORDER BY LastUpdated DESC;

-- Batches still "stuck" in Processing (would indicate an unhandled crash mid-import,
-- since a completed/rolled-back run should never leave this state)
SELECT * FROM ImportBatches WHERE Status = 'Processing';

-- Batches that completed with row-level errors
SELECT ImportBatchId, FileName, FailedRows, StartedDate
FROM ImportBatches
WHERE Status = 'CompletedWithErrors'
ORDER BY StartedDate DESC;

-- Duplicate-detection sanity check: any products sharing the same non-null Barcode?
SELECT Barcode, COUNT(*) FROM Products
WHERE Barcode IS NOT NULL
GROUP BY Barcode
HAVING COUNT(*) > 1;
```

### 9.5 Notes
- Matching precedence is **`ExternalProductId` first, `Barcode` second** — if a vendor reuses a barcode across SKUs but supplies distinct `ExternalProductId`s, they'll correctly create/update separate products.
- `Description` and `ImageUrl` are **overwritten unconditionally** on update (even with blank values), whereas `Barcode` and `Currency` are only overwritten if the new value is non-blank. Keep this asymmetry in mind when re-running imports with partial data.

---

## 10. Module: Inventory (`InventoryController`)

**Files:** `Controllers/InventoryController.cs`, `Services/InventoryService.cs`, `DTOs/Inventory/InventoryResponseDto.cs`
**Tables touched:** `Inventories`, `Products` (existence check)

### 10.1 Purpose
Exposes stock levels per product and lets warehouse staff correct/restock them. Also the single source of truth `IsLowStock` uses to flag reorder needs.

### 10.2 API Endpoints — all `[Authorize]`; write endpoints additionally require `Admin` or `WarehouseManager` role.

| Method & Route | Auth | Input | Output |
|---|---|---|---|
| `GET /api/inventory/{productId}` | Any authenticated user | route `productId` | `200 OK` → `InventoryResponseDto` · `404` |
| `PUT /api/inventory/{productId}` | `Admin,WarehouseManager` | body `UpdateInventoryRequest`: `QuantityAvailable`, `QuantityReserved`, `SafetyStockLevel`, `ProcurementLeadTimeDays` (all replace, not delta) | `200 OK` → updated DTO · `400` (negative values, or reserved > available) · `404` if product doesn't exist |
| `POST /api/inventory/{productId}/restock` | `Admin,WarehouseManager` | body `RestockRequest`: `Quantity` (added, not set) | `200 OK` → updated DTO · `400` if `Quantity <= 0` · `404` if no inventory row exists yet |

`InventoryResponseDto`: `InventoryId`, `ProductId`, `QuantityAvailable`, `QuantityReserved`, `SafetyStockLevel`, `ProcurementLeadTimeDays`, `LastUpdated`, computed `QuantityAfterReservation = QuantityAvailable - QuantityReserved`, computed `IsLowStock = QuantityAfterReservation <= SafetyStockLevel`.

### 10.3 Flow — Update
1. Reject negative numbers, and reject `QuantityReserved > QuantityAvailable`.
2. If no `Inventory` row exists for the product: verify the product itself exists (and isn't soft-deleted) — 404 if not — then **create** the inventory row with the given values.
3. If a row exists, overwrite all four fields directly (full replace semantics, not incremental).

### 10.4 Flow — Restock
1. Reject `Quantity <= 0`.
2. Requires an **existing** `Inventory` row (404 if none — unlike `Update`, this endpoint won't create one).
3. `QuantityAvailable += Quantity` (additive, unlike `Update`).

### 10.5 SSMS Verification Queries

```sql
-- Full stock picture with computed low-stock flag (mirrors the DTO's computed properties)
SELECT p.ProductId, p.Name,
       i.QuantityAvailable, i.QuantityReserved,
       (i.QuantityAvailable - i.QuantityReserved) AS QuantityAfterReservation,
       i.SafetyStockLevel,
       CASE WHEN (i.QuantityAvailable - i.QuantityReserved) <= i.SafetyStockLevel
            THEN 1 ELSE 0 END AS IsLowStock,
       i.ProcurementLeadTimeDays, i.LastUpdated
FROM Inventories i
JOIN Products p ON p.ProductId = i.ProductId
ORDER BY IsLowStock DESC, QuantityAfterReservation ASC;

-- All products currently flagged low-stock (candidates for procurement/restock)
SELECT p.Name, i.QuantityAvailable, i.QuantityReserved, i.SafetyStockLevel
FROM Inventories i
JOIN Products p ON p.ProductId = i.ProductId
WHERE (i.QuantityAvailable - i.QuantityReserved) <= i.SafetyStockLevel;

-- Products with no inventory row at all (can't be added to a cart until one exists)
SELECT p.ProductId, p.Name
FROM Products p
LEFT JOIN Inventories i ON i.ProductId = p.ProductId
WHERE i.InventoryId IS NULL AND p.IsDeleted = 0;

-- Sanity check: reserved should never exceed available (app-enforced, verify DB agrees)
SELECT * FROM Inventories WHERE QuantityReserved > QuantityAvailable;
```

### 10.6 Notes
- There is **no dedicated endpoint to decrement stock manually** — stock is only reduced by the Payment module when a payment succeeds (see [§12](#12-module-payments-stripe-paymentcontroller--stripewebhookcontroller)), or overwritten wholesale via `PUT`.
- `QuantityReserved` is **never incremented anywhere in the current codebase** when an order is created or an item is added to a cart — it is only ever *decremented* (in the payment-success flow) or *set directly* via the `PUT` endpoint or a vendor import. In practice this means "available for sale" (`QuantityAvailable - QuantityReserved`) will drift positive relative to reality unless something else in your workflow calls `PUT /api/inventory/{productId}` to set `QuantityReserved` explicitly when an order is placed. Worth validating against your actual business process.

---

## 11. Module: Orders (`OrderController`)

**Files:** `Controllers/OrderController.cs`, `Services/OrderService.cs`, `DTOs/Orders/*`
**Tables touched:** `Orders`, `OrderItems`, `Carts`/`CartItems` (cart is emptied), `Products`, `Inventories` (read-only), `auditLogs`

### 11.1 Purpose
Converts the caller's current cart into an `Order` (a checkout), validating stock and calculating an estimated delivery date per line item and overall.

### 11.2 API Endpoints — all `[Authorize]`

| Method & Route | Input | Output |
|---|---|---|
| `POST /api/orders` | — (uses caller's cart) | `201 Created` (Location → `GetOrderById`) → `CreateOrderResponseDto` · `400` if cart empty or a product unavailable/insufficient stock |
| `GET /api/orders` | — | `200 OK` → `List<OrderResponseDto>`, newest first |
| `GET /api/orders/{orderId}` | route `orderId` | `200 OK` → `OrderResponseDto` (only if it belongs to caller) · `404` |

`CreateOrderResponseDto`: `OrderId`, `OrderStatus`, `TotalAmount`, `ExpectedDeliveryDate`.
`OrderResponseDto`: `OrderId`, `UserId`, `OrderStatus`, `TotalAmount`, `ExpectedDeliveryDate`, `CreatedDate`, `Items[]` (`OrderItemResponseDto`: `OrderItemId`, `ProductId`, `ProductName`, `Quantity`, `UnitPrice`, computed `Subtotal`, `DeliveryDays`).

### 11.3 Flow — Create Order
1. Load the caller's cart with items + products. If empty/missing → returns `null` → controller returns `400` "Cart is empty."
2. Batch-load `Inventory` rows for every product in the cart.
3. **Per cart line**: the product must exist, not be soft-deleted, and be `Active` — else throws `InvalidOperationException` (→400, and the whole order creation aborts, nothing is saved). Inventory must exist for the product, and `Quantity ≤ (QuantityAvailable - QuantityReserved)` — else throws (→400).
4. `unitPrice` is copied from the **current** `Product.Price` at checkout time (frozen into `OrderItem.UnitPrice`, so future price changes don't retroactively affect placed orders).
5. `deliveryDays` per line = **2 days** if stock after reservation is above the safety-stock level; otherwise `Max(2, ProcurementLeadTimeDays + 2)` days. The order's overall `ExpectedDeliveryDate` = today + the **maximum** delivery days across all lines.
6. Sum `unitPrice * quantity` into `TotalAmount`.
7. Insert the `Order` (`OrderStatus = Pending`) with its `OrderItems` in one call, and **remove all `CartItems`** from the cart (the `Cart` row itself survives, now empty) — one `SaveChangesAsync()`.
8. Write an `AuditLog` row (`Action = "OrderCreated"`, includes the formatted total amount in `NewValue`).

### 11.4 Flow — Get Orders / Get Order By Id
Read-only, scoped to `o.UserId == callerId` — a user can never see another user's orders (not even by guessing an id: `GetOrderById` returns 404, not 403, when the order exists but belongs to someone else).

### 11.5 SSMS Verification Queries

```sql
-- All orders for a user, most recent first
SELECT o.OrderId, o.OrderStatus, o.TotalAmount, o.ExpectedDeliveryDate, o.CreatedDate
FROM Orders o
JOIN Users u ON u.UserId = o.UserId
WHERE u.Email = 'someone@example.com'
ORDER BY o.CreatedDate DESC;

-- Full order detail with line items
SELECT o.OrderId, o.OrderStatus, o.TotalAmount, o.ExpectedDeliveryDate,
       oi.ProductId, p.Name, oi.Quantity, oi.UnitPrice,
       (oi.UnitPrice * oi.Quantity) AS Subtotal, oi.DeliveryDays
FROM Orders o
JOIN OrderItems oi ON oi.OrderId = o.OrderId
JOIN Products p ON p.ProductId = oi.ProductId
WHERE o.OrderId = 1001;

-- Verify TotalAmount always equals the sum of its line items (should be 0 rows)
SELECT o.OrderId, o.TotalAmount, SUM(oi.UnitPrice * oi.Quantity) AS ComputedTotal
FROM Orders o
JOIN OrderItems oi ON oi.OrderId = o.OrderId
GROUP BY o.OrderId, o.TotalAmount
HAVING o.TotalAmount <> SUM(oi.UnitPrice * oi.Quantity);

-- Orders by status funnel
SELECT OrderStatus, COUNT(*) AS Count, SUM(TotalAmount) AS Revenue
FROM Orders
GROUP BY OrderStatus
ORDER BY Count DESC;

-- Orders still unpaid (Pending) older than 24h — candidates for a reminder job
SELECT o.OrderId, u.Email, o.TotalAmount, o.CreatedDate
FROM Orders o
JOIN Users u ON u.UserId = o.UserId
WHERE o.OrderStatus = 'Pending' AND o.CreatedDate < DATEADD(HOUR, -24, SYSUTCDATETIME());

-- Confirm a checkout actually emptied the cart (should show 0 items for the order's user right after)
SELECT c.CartId, COUNT(ci.CartItemId) AS ItemsRemaining
FROM Carts c
LEFT JOIN CartItems ci ON ci.CartId = c.CartId
WHERE c.UserId = (SELECT UserId FROM Users WHERE Email = 'someone@example.com')
GROUP BY c.CartId;
```

### 11.6 Notes
- Order creation is **all-or-nothing**: if any single line item fails validation (unavailable product / insufficient stock), the entire order fails — no partial order is created.
- `OrderStatus` only becomes `Confirmed` when a payment succeeds (see next module); it never becomes anything else automatically in the current code (no `Shipped`/`Delivered`/`Cancelled` transition exists yet outside of what you might do by hand in SQL or a future admin endpoint).

---

## 12. Module: Payments (Stripe) (`PaymentController` / `StripeWebhookController`)

**Files:** `Controllers/PaymentController.cs`, `Controllers/StripeWebhookController.cs`, `Services/PaymentService.cs`, `DTOs/Payments/*`, `Models/Configuration/StripeSettings.cs`
**Tables touched:** `Payment`, `Orders`, `OrderItems` (read), `Inventories` (write — stock decrement), `auditLogs`
**External dependency:** Stripe API (`Stripe.net` SDK)

### 12.1 Purpose
Creates a Stripe `PaymentIntent` for an order's total, and confirms payment success either via (a) Stripe's real webhook, or (b) a **test-mode confirmation endpoint** that directly confirms the PaymentIntent server-side (useful for automated tests / sandbox flows without a real browser+Stripe.js round trip).

### 12.2 API Endpoints

| Method & Route | Auth | Input | Output |
|---|---|---|---|
| `POST /api/payments/create` | `[Authorize]`, rate-limited (`PaymentPolicy` — **not configured**, see [Known Gaps](#19-known-gaps--behavior-notes-read-before-debugging)) | `CreatePaymentRequestDto`: `OrderId` | `200 OK` → `PaymentResponseDto` (incl. Stripe `ClientSecret`) · `404` order not found · `400` (order total ≤ 0, order cancelled, already paid, Stripe key missing) |
| `POST /api/payments/confirm-test` | `[Authorize]`, rate-limited (`PaymentPolicy`) | `ConfirmTestPaymentRequestDto`: `PaymentId`, `TestPaymentMethod` (default `pm_card_visa`) | `200 OK` → `PaymentResponseDto` · `404` payment not found · `400` various |
| `POST /api/payments/webhook` | Anonymous (Stripe calls this directly; verified via signature, not JWT) | Raw JSON body + `Stripe-Signature` header | `200 OK` (empty) · `400` if signature missing/invalid |

`PaymentResponseDto`: `PaymentId`, `OrderId`, `StripePaymentIntentId`, `Amount`, `Currency`, `PaymentStatus`, `ClientSecret?`, `FailureReason?`, `CreatedDate`, `UpdatedDate`.

### 12.3 Flow — Create Payment Intent
1. Load the order (must belong to caller). 404 if not found.
2. Reject if `TotalAmount <= 0`, or order is `Cancelled`, or a `Succeeded` payment already exists for it (400 each case — no double-charging).
3. **If a `Pending`/`Processing` payment already exists** for the order, don't create a new Stripe PaymentIntent — just re-fetch the existing one from Stripe and return its (still valid) `ClientSecret` (idempotent retry behavior).
4. Otherwise create a new Stripe `PaymentIntent` for `Round(TotalAmount * 100)` cents, currency `usd`, metadata `{orderId, userId}`, automatic payment methods enabled with redirects disabled.
5. Persist a new `Payment` row (`PaymentStatus = Pending`) linked to the Stripe PaymentIntent id.
6. Return the DTO including the **`ClientSecret`** — this is what a frontend would feed into Stripe.js/Stripe Elements to complete payment in the browser.

### 12.4 Flow — Stripe Webhook (`payment_intent.succeeded`)
1. Verify the raw JSON body's signature against `Stripe:WebhookSecret` using `Stripe.EventUtility.ConstructEvent` — invalid/missing signature → 400, and the event is **not** trusted or processed.
2. Ignore all event types except `payment_intent.succeeded`.
3. Look up the local `Payment` row by `StripePaymentIntentId`. If not found, or already `Succeeded`, no-op (idempotent — Stripe retries webhooks, this must be safe to receive twice).
4. If the order is `Cancelled`, throw (400) — a payment shouldn't be able to complete a cancelled order.
5. **Inventory settlement**, inside a DB transaction: for every `OrderItem`, verify `QuantityAvailable - QuantityReserved >= item.Quantity` (throws if not — meaning stock evaporated between order creation and payment), then for each item: `QuantityAvailable -= Quantity` (permanently consume stock) and `QuantityReserved = Max(0, QuantityReserved - Quantity)`.
6. Mark `Payment.PaymentStatus = Succeeded`, `Order.OrderStatus = Confirmed`.
7. Send a confirmation email to the order owner via `IEmailService` (subject: `Retalon Order #{id} Confirmed`).
8. Commit.

### 12.5 Flow — Confirm Test Payment (`/confirm-test`)
Same inventory-settlement + status-update + email logic as the webhook, but triggered synchronously by the authenticated caller instead of by Stripe, and additionally:
- Verifies the payment belongs to the caller's own order.
- Calls Stripe's `PaymentIntentService.ConfirmAsync` directly with the given test payment method (e.g. `pm_card_visa`) — this actually attempts to confirm the PaymentIntent against Stripe (typically in Stripe *test mode*).
- If Stripe reports anything other than `"succeeded"`, throws 400 with Stripe's status in the message.
- Also writes an `AuditLog` entry (`Action = "PaymentSucceeded"`) — **the webhook path does not write an AuditLog entry for the same event**, only this test-confirm path does (see [Known Gaps](#19-known-gaps--behavior-notes-read-before-debugging)).

### 12.6 SSMS Verification Queries

```sql
-- All payments with their order + owner
SELECT pay.PaymentId, pay.StripePaymentIntentId, pay.Amount, pay.Currency,
       pay.PaymentStatus, pay.FailureReason, pay.CreatedDate, pay.UpdatedDate,
       o.OrderId, o.OrderStatus, u.Email
FROM Payment pay
JOIN Orders o ON o.OrderId = pay.OrderId
JOIN Users u ON u.UserId = o.UserId
ORDER BY pay.CreatedDate DESC;

-- Payments stuck in Pending/Processing (never confirmed — abandoned checkout)
SELECT pay.PaymentId, o.OrderId, u.Email, pay.Amount, pay.CreatedDate
FROM Payment pay
JOIN Orders o ON o.OrderId = pay.OrderId
JOIN Users u ON u.UserId = o.UserId
WHERE pay.PaymentStatus IN ('Pending', 'Processing')
ORDER BY pay.CreatedDate;

-- Verify: every Succeeded payment has its order in Confirmed (or later) status
SELECT pay.PaymentId, pay.PaymentStatus, o.OrderId, o.OrderStatus
FROM Payment pay
JOIN Orders o ON o.OrderId = pay.OrderId
WHERE pay.PaymentStatus = 'Succeeded' AND o.OrderStatus = 'Pending';
-- ^ should return 0 rows; if not, inventory/status update failed partway

-- Verify: no order has two Succeeded payments (double payment)
SELECT OrderId, COUNT(*) AS SucceededPayments
FROM Payment
WHERE PaymentStatus = 'Succeeded'
GROUP BY OrderId
HAVING COUNT(*) > 1;

-- Revenue by day
SELECT CAST(CreatedDate AS DATE) AS Day, SUM(Amount) AS Revenue, COUNT(*) AS Payments
FROM Payment
WHERE PaymentStatus = 'Succeeded'
GROUP BY CAST(CreatedDate AS DATE)
ORDER BY Day DESC;

-- Uniqueness check on StripePaymentIntentId (enforced by DB unique index — verify it held)
SELECT StripePaymentIntentId, COUNT(*) FROM Payment
GROUP BY StripePaymentIntentId HAVING COUNT(*) > 1;
```

### 12.7 Notes
- `Stripe:SecretKey` and `Stripe:WebhookSecret` must be configured (`appsettings.json` ships them blank) — every payment call will 400 with "Stripe test secret key is not configured." until you set them (typically via `appsettings.Development.json`, user secrets, or environment variables — never commit real keys).
- The webhook endpoint is intentionally `[AllowAnonymous]` — it is protected by **signature verification**, not JWT, because Stripe (not your frontend) calls it.
- Amounts are converted to Stripe's smallest currency unit (cents) via `Round(Amount * 100)`; be careful with currencies that don't use 2 decimal places if you ever add more than USD.

---

## 13. Module: Procurement (`ProcurementController`)

**Files:** `Controllers/ProcurementController.cs`, `Services/ProcurementService.cs`, `DTOs/Procurements/*`
**Tables touched:** `Procurements`, `Orders`, `OrderItems` (read), `Inventories` (read only — does **not** modify stock)

### 13.1 Purpose
When an order's line item quantity exceeds what's currently available, this raises a `Procurement` record (a "please reorder this much stock" ticket) so warehouse/purchasing staff can act on it and track its lifecycle to `Received`/`Completed`.

### 13.2 API Endpoints — all `[Authorize]`; status updates additionally require `Admin`/`WarehouseManager`.

| Method & Route | Input | Output |
|---|---|---|
| `POST /api/procurement` | `CreateProcurementRequestDto`: `OrderId` | `200 OK` → `List<ProcurementResponseDto>` (one per short-stocked line item; empty list if nothing was short) · `400` if order not found/not owned |
| `GET /api/procurement` | — | `200 OK` → all procurements for the caller's own orders, newest first |
| `GET /api/procurement/{procurementId}` | route id | `200 OK` → single record (only if tied to caller's order) · `404` |
| `PUT /api/procurement/{procurementId}/status` | body: raw `ProcurementStatus` enum value (as JSON string, e.g. `"Ordered"`); `Admin,WarehouseManager` only | `200 OK` → updated record · `404` |

`ProcurementResponseDto`: `ProcurementId`, `OrderId`, `ProductId`, `RequiredQuantity`, `ProcurementStatus`, `ExpectedArrivalDate?`, `CreatedDate`, `UpdatedDate`.

### 13.3 Flow — Create Procurement
1. Load the order (with items), scoped to caller. 404-equivalent (`ArgumentException` → 400) if not theirs/not found.
2. **Per order line item**: load its `Inventory`; skip silently if no inventory row exists. Compute `shortage = item.Quantity - (QuantityAvailable - QuantityReserved)`.
3. Skip the line if `shortage <= 0` (there's enough stock — no procurement needed for it).
4. If a procurement for this exact `(OrderId, ProductId)` already exists and isn't `Completed`/`Cancelled`, **reuse it** (don't duplicate) rather than inserting a second ticket.
5. Otherwise insert a new `Procurement` (`ProcurementStatus = Requested`, `RequiredQuantity = shortage`).
6. Return every procurement touched (existing-reused + newly-created) for this call.

### 13.4 Flow — Update Status
Directly sets `ProcurementStatus` to whatever value is passed (`Pending`, `Requested`, `Ordered`, `Received`, `Cancelled`, `Completed`) and stamps `UpdatedDate`. **No transition validation** — you can jump straight from `Requested` to `Completed`, or backward from `Received` to `Pending`; the API does not enforce a state machine.

### 13.5 SSMS Verification Queries

```sql
-- Open procurement tickets (need action)
SELECT pr.ProcurementId, o.OrderId, p.Name AS Product, pr.RequiredQuantity,
       pr.ProcurementStatus, pr.ExpectedArrivalDate, pr.CreatedDate
FROM Procurements pr
JOIN Orders o ON o.OrderId = pr.OrderId
JOIN Products p ON p.ProductId = pr.ProductId
WHERE pr.ProcurementStatus NOT IN ('Completed', 'Cancelled')
ORDER BY pr.CreatedDate;

-- Procurement funnel by status
SELECT ProcurementStatus, COUNT(*) AS Count, SUM(RequiredQuantity) AS TotalUnitsNeeded
FROM Procurements
GROUP BY ProcurementStatus;

-- Duplicate-ticket sanity check (should reflect the "reuse if not Completed/Cancelled" rule —
-- i.e. multiple ACTIVE tickets for the same order+product would be a bug)
SELECT OrderId, ProductId, COUNT(*) AS ActiveTickets
FROM Procurements
WHERE ProcurementStatus NOT IN ('Completed', 'Cancelled')
GROUP BY OrderId, ProductId
HAVING COUNT(*) > 1;

-- Which products most frequently need procurement (recurring shortage candidates
-- for raising SafetyStockLevel)
SELECT p.Name, COUNT(*) AS TimesRequested, SUM(pr.RequiredQuantity) AS TotalShortage
FROM Procurements pr
JOIN Products p ON p.ProductId = pr.ProductId
GROUP BY p.Name
ORDER BY TimesRequested DESC;
```

### 13.6 Notes
- Creating a procurement never touches `Inventories` — it's purely a tracking ticket. Whoever updates the ticket to `Received`/`Completed` is expected to separately call the Inventory module's `restock`/`PUT` endpoints to actually add the stock.
- Because status updates have no workflow enforcement, if you need strict state transitions (e.g. disallow `Completed → Requested`), that has to be added — it isn't there today.

---

## 14. Module: Notifications & Email (`NotificationsController`)

**Files:** `Controllers/NotificationsController.cs`, `Services/EmailService.cs`, `Models/Configuration/EmailSettings.cs`, `DTOs/Notifications/SendTestEmailRequestDto.cs`
**Tables touched:** none directly by this controller (see notes — the `Notifications` table exists but isn't written to by any current code path)

### 14.1 Purpose
A thin wrapper to send an arbitrary plain-text email via SMTP (MailKit) — used directly for a manual "test email" endpoint, and internally by `PaymentService` to send order-confirmation emails.

### 14.2 API Endpoint

| Method & Route | Auth | Input | Output |
|---|---|---|---|
| `POST /api/notifications/test-email` | `[Authorize]` | `SendTestEmailRequestDto`: `ToEmail` (required), `Subject` (default `"Retalon Test Email"`), `Body` (default `"This is a test email from Retalon."`) | `200 OK { message: "Test email sent successfully." }` · `400` if `ToEmail` blank |

### 14.3 Flow
1. Validate `ToEmail` is non-blank.
2. Build a MIME message from `Email:FromName`/`Email:FromEmail` config → `ToEmail`, plain-text body.
3. Connect to `Email:SmtpServer:SmtpPort` (StartTLS if `EnableSsl=true`), authenticate if a username is configured, send, disconnect.
4. Throws `InvalidOperationException` (→400 via global handler) if `Email:SmtpServer` isn't configured.

### 14.4 SSMS Verification Queries

The `Notifications` table exists in the schema (originally intended to log in-app notifications with `NotificationType`/`NotificationStatus`), but as of this codebase **no service inserts rows into it** — email sending happens live via SMTP without a durable record. These queries will simply confirm that:

```sql
-- Confirm the table is currently unused (expect 0 rows unless someone manually inserted test data)
SELECT COUNT(*) AS NotificationRowCount FROM Notifications;

-- If/when you wire up persistence, this is the shape to query:
SELECT n.NotificationId, u.Email, n.NotificationType, n.NotificationStatus,
       n.Message, n.SentDate, n.CreatedDate
FROM Notifications n
JOIN Users u ON u.UserId = n.UserId
ORDER BY n.CreatedDate DESC;
```

### 14.5 Notes
- `Email:*` settings ship blank in `appsettings.json` — configure them (e.g., in `appsettings.Development.json` or environment variables) before testing this module or the payment-confirmation email flow.
- Because there's no `Notifications` row created, there is currently **no way to query "what emails were sent to whom and when" from SQL** — that trail only exists in the Serilog file/console logs (`Logs/log-*.txt`) if you added logging around `EmailService`, or in your SMTP provider's own sent-mail log.

---

## 15. Module: Audit Logging (`AuditService`)

**Files:** `Services/AuditService.cs`, `Services/Interfaces/IAuditService.cs`, `Models/Entities/AuditLog.cs`
**Table:** `auditLogs` (lowercase — note this when writing raw SQL)
**Called from:** `AuthService.LoginAsync` (`Action = "UserLogin"`), `OrderService.CreateOrderAsync` (`Action = "OrderCreated"`), `PaymentService.ConfirmTestPaymentAsync` (`Action = "PaymentSucceeded"`)

### 15.1 Purpose
A generic, freeform audit trail: "who did what to which entity, and what changed." Not tied to any specific endpoint — it's an internal cross-cutting service any other service can call.

### 15.2 Method Signature
```csharp
Task LogAsync(Guid? userId, string action, string? entityName = null,
              string? entityId = null, string? details = null,
              CancellationToken cancellationToken = default)
```
Internally, `entityId` is **not persisted as a separate column** — only `action`, `entityName`, and `details` (stored as `NewValue`) end up in the row, along with `UserId` and `PerformedByUserId` (both set to the same `userId` passed in — there's no concept of "acting on behalf of another user" actually exercised yet, even though the column exists for it).

### 15.3 SSMS Verification Queries

```sql
-- All audit log entries, most recent first
SELECT al.AuditLogId, u.Email, al.Action, al.EntityName, al.NewValue, al.Timestamp
FROM auditLogs al
LEFT JOIN Users u ON u.UserId = al.UserId
ORDER BY al.Timestamp DESC;

-- Audit trail for a specific user
SELECT Action, EntityName, NewValue, Timestamp
FROM auditLogs
WHERE UserId = (SELECT UserId FROM Users WHERE Email = 'someone@example.com')
ORDER BY Timestamp DESC;

-- Count of each audit action type (feature usage at a glance)
SELECT Action, COUNT(*) AS Occurrences
FROM auditLogs
GROUP BY Action
ORDER BY Occurrences DESC;

-- Logins per day (using the audit trail rather than LastLoginDate, to see history not just latest)
SELECT CAST(Timestamp AS DATE) AS Day, COUNT(*) AS Logins
FROM auditLogs
WHERE Action = 'UserLogin'
GROUP BY CAST(Timestamp AS DATE)
ORDER BY Day DESC;
```

### 15.4 Notes
- `OldValue`, `IpAddress`, `CorrelationId`, `RequestId` columns exist on the entity/table but are **never populated** by any current caller (always `NULL`) — the plumbing is there for future use (e.g., middleware that stamps a correlation id per request) but isn't wired up yet.
- Remember the table name is **`auditLogs`** (all lowercase `a`), not `AuditLogs` — SQL Server is case-insensitive by default so it usually won't matter, but scripts/ORMs that assume PascalCase table names elsewhere should double check this one.

---

## 16. Module: Security Event Logging (`SecurityEventService`)

**Files:** `Services/SecurityEventService.cs`, `Models/Entities/SecurityEvent.cs`
**Table:** `SecurityEvents`
**Called from:** `AuthService` only — `FailedLogin` (bad password), `AccountLocked` (5th failed attempt), `TokenRevoked` (logout)

### 16.1 Purpose
A narrower, security-specific sibling of the audit log — tracks authentication/authorization anomalies specifically (see [`SecurityEventType`](#18-enums-reference) for the full enum, though only 3 of its 6 values are currently raised by any code: `FailedLogin`, `AccountLocked`, `TokenRevoked`. `UnauthorizedAccess`, `RateLimitExceeded`, `SuspiciousActivity` exist in the enum but nothing in the codebase currently logs them).

### 16.2 Method Signature
```csharp
Task LogAsync(Guid? userId, SecurityEventType securityEventType, string description,
              string? ipAddress = null, CancellationToken cancellationToken = default)
```
Note: `ipAddress` is a parameter but **no caller currently passes one** — every row in this table today will have `IpAddress = NULL` in practice, even though the column and index exist for it.

### 16.3 SSMS Verification Queries

```sql
-- All security events, most recent first
SELECT se.SecurityEventId, u.Email, se.SecurityEventType, se.Description,
       se.IpAddress, se.CreatedDate
FROM SecurityEvents se
LEFT JOIN Users u ON u.UserId = se.UserId
ORDER BY se.CreatedDate DESC;

-- Brute-force detection: users with the most failed logins in the last 24h
SELECT u.Email, COUNT(*) AS FailedAttempts
FROM SecurityEvents se
JOIN Users u ON u.UserId = se.UserId
WHERE se.SecurityEventType = 'FailedLogin'
  AND se.CreatedDate > DATEADD(HOUR, -24, SYSUTCDATETIME())
GROUP BY u.Email
ORDER BY FailedAttempts DESC;

-- Accounts that have ever been locked
SELECT DISTINCT u.Email, se.CreatedDate
FROM SecurityEvents se
JOIN Users u ON u.UserId = se.UserId
WHERE se.SecurityEventType = 'AccountLocked'
ORDER BY se.CreatedDate DESC;

-- Event type distribution
SELECT SecurityEventType, COUNT(*) FROM SecurityEvents GROUP BY SecurityEventType;
```

---

## 17. End-to-End Flow: "Add to cart → Checkout → Pay → Fulfill"

This ties every module above into one narrative, matching what actually happens in the code, table by table:

1. **User registers** → `Users` + `UserRoles` (Customer) + empty `Carts` row created together.
2. **User logs in** → JWT issued; `RefreshTokens` row created; `auditLogs` "UserLogin" row written.
3. **User searches a product** (`GET /api/products/search`) → if found locally, served from cache/`Products`; if not, silently pulled from Open Food Facts into `Products` + `Inventories` (qty 0).
4. **User adds it to cart** (`POST /api/cart/items`) → checked against `Inventories.QuantityAvailable - QuantityReserved`; upserts a row in `CartItems`.
5. **User checks out** (`POST /api/orders`) → cart validated line-by-line against current stock and status; `Orders` + `OrderItems` inserted (status `Pending`, `TotalAmount` frozen, `ExpectedDeliveryDate` computed); `CartItems` for that cart deleted; `auditLogs` "OrderCreated" row written.
6. **(Optional) Insufficient stock detected** (`POST /api/procurement`) → for any line where demand > availability, a `Procurements` row is raised (`Requested`) — purely informational, doesn't block or unblock the order.
7. **User pays** (`POST /api/payments/create`) → Stripe `PaymentIntent` created; `Payment` row inserted (`Pending`), `ClientSecret` returned to the frontend to complete payment with Stripe.js — **or**, in test/sandbox flows, `POST /api/payments/confirm-test` confirms it directly server-side.
8. **Payment succeeds** (via real Stripe webhook `POST /api/payments/webhook`, or the test-confirm endpoint) → inside one transaction: `Inventories.QuantityAvailable` is decremented and `QuantityReserved` decremented (floor 0) per line item; `Payment.PaymentStatus = Succeeded`; `Order.OrderStatus = Confirmed`; a confirmation email is sent; (test-confirm path only) `auditLogs` "PaymentSucceeded" row written.
9. **Warehouse fulfills the order** — there is currently **no API endpoint** to progress `OrderStatus` beyond `Confirmed` (no `Shipped`/`Delivered` transition exists in code) or to progress `ProcurementStatus` beyond manual `PUT /api/procurement/{id}/status` calls. Any further lifecycle would currently be done directly in SQL or needs a new endpoint.

---

## 18. Enums Reference

All stored as `nvarchar` strings in the DB (via EF's `HasConversion<string>()`), so filter with the string literal, not a number.

| Enum | Values | Where used |
|---|---|---|
| `ProductStatus` | `Active`, `Inactive`, `Discontinued` | `Products.ProductStatus` |
| `OrderStatus` | `Pending`, `Confirmed`, `Processing`, `Shipped`, `Delivered`, `Cancelled` | `Orders.OrderStatus` (code currently only ever sets `Pending`→`Confirmed`; the rest are unused by any endpoint today) |
| `PaymentStatus` | `Pending`, `Processing`, `Succeeded`, `Failed`, `Canceled`, `Refunded` | `Payment.PaymentStatus` (code sets `Pending` and `Succeeded`; `Processing`/`Failed`/`Canceled`/`Refunded` aren't set by any current path) |
| `ProcurementStatus` | `Pending`, `Requested`, `Ordered`, `Received`, `Cancelled`, `Completed` | `Procurements.ProcurementStatus` (creation always sets `Requested`; all others only reachable via the manual status-update endpoint) |
| `ImportBatchStatus` | `Processing`, `Completed`, `CompletedWithErrors`, `Failed` | `ImportBatches.Status` (`Failed` is never explicitly set in code — an exception mid-import rolls back the whole transaction instead of leaving a `Failed` row) |
| `NotificationType` | `OrderCreated`, `PaymentSuccess`, `OrderShipped`, `OrderDelivered`, `ProcurementCreated`, `LowStock` | `Notifications.NotificationType` (table currently unused — see [§14](#14-module-notifications--email-notificationscontroller)) |
| `NotificationStatus` | `Pending`, `Sent`, `Failed`, `Read` | `Notifications.NotificationStatus` (same — unused) |
| `SecurityEventType` | `FailedLogin`, `AccountLocked`, `UnauthorizedAccess`, `RateLimitExceeded`, `TokenRevoked`, `SuspiciousActivity` | `SecurityEvents.SecurityEventType` (only `FailedLogin`, `AccountLocked`, `TokenRevoked` are ever logged today) |

Seeded roles (`Roles` table, fixed GUIDs from the `SeedRoles` migration):

| RoleId | Name | Description |
|---|---|---|
| `11111111-1111-1111-1111-111111111111` | `Admin` | System administrator |
| `22222222-2222-2222-2222-222222222222` | `Customer` | Customer user (auto-assigned on `/register`) |
| `33333333-3333-3333-3333-333333333333` | `WarehouseManager` | Warehouse manager |

```sql
-- Handy: promote a user to Admin directly in SQL (for local/dev testing)
INSERT INTO UserRoles (UserId, RoleId)
SELECT UserId, '11111111-1111-1111-1111-111111111111'
FROM Users
WHERE Email = 'someone@example.com'
  AND UserId NOT IN (
      SELECT UserId FROM UserRoles WHERE RoleId = '11111111-1111-1111-1111-111111111111'
  );
```

---

## 19. Known Gaps / Behavior Notes (read before debugging)

These are real, observed characteristics of the current code — useful to know before you assume something is "broken" when testing:

1. **`PaymentPolicy` rate-limit policy is referenced but not registered.** `PaymentController` decorates both its endpoints with `[EnableRateLimiting("PaymentPolicy")]`, but `Program.cs` only registers `LoginPolicy` and `SearchPolicy` via `AddFixedWindowLimiter`. Calling either payment endpoint may throw at request-pipeline time because the named policy doesn't exist. Fix by adding a `PaymentPolicy` limiter in `Program.cs`, or removing the attribute.
2. **`Notifications` and `SearchHistories` tables are unused.** Nothing in the current codebase inserts into them, even though they're fully modeled (entities, Fluent config, migrations). If you're trying to find "notification history" or "what did users search for," it isn't there yet — check Serilog logs instead.
3. **`QuantityReserved` is never incremented.** Cart-add and order-creation both *read* `QuantityReserved` to compute availability, but nothing writes to it upward; it only ever goes down (payment-success settlement) or gets set directly (Inventory `PUT`, vendor import). If your workflow assumes "reserved" tracks in-flight unpaid orders automatically, it currently does not — you'd need to add that write, likely in `OrderService.CreateOrderAsync`.
4. **Refresh-token/logout lookups are O(n) BCrypt scans.** Because tokens are hashed, `RefreshTokenAsync` and `LogoutAsync` load *all* currently-valid tokens and BCrypt-verify one by one until they find a match — fine for a handful of sessions, but something to be aware of if you load-test with many concurrent sessions per user.
5. **Registration silently drops `AddressLine2`, `State`, `PhoneNumber`.** The `RegisterRequestDto` accepts them, but `User` entity has no matching columns — only `Address` (=`AddressLine1`), `City`, `PostalCode`, `Country` persist.
6. **`GET /api/products/barcode/{barcode}` does not persist Open Food Facts fallback results** — only the `POST /api/products/import/{barcode}` and search-fallback paths write new `Products` rows. A plain barcode `GET` for an unknown local barcode will hit the external API on *every single call* (not even cached, since the cache-set line only triggers on a genuine miss-then-fetch, and it is in fact cached for 5 minutes — but nothing is saved to SQL).
7. **`ProductController.Import` (single barcode import) doesn't create an `Inventory` row**, while the search-fallback auto-import path does (with all-zero quantities). A product imported via `POST /api/products/import/{barcode}` won't be addable to a cart until you `PUT`/`POST restock` its inventory (because `CartService.AddItemAsync` requires an existing `Inventory` row).
8. **`Orders`/`Procurements`/`ProcurementStatus` transitions are unrestricted.** `PUT /api/procurement/{id}/status` accepts any enum value with no state-machine validation (e.g., `Completed → Pending` is accepted). Similarly, there's no endpoint at all to move `OrderStatus` past `Confirmed`.
9. **The webhook path and the test-confirm path both settle inventory/payment identically, but only the test-confirm path writes an `auditLogs` entry** (`"PaymentSucceeded"`). A payment confirmed via the real Stripe webhook in production will **not** show up in `auditLogs` — only in `Payment`/`Orders` row updates and Serilog output.
10. **`ImportBatchStatus.Failed` is never actually set.** If a vendor import throws partway through, the entire DB transaction (including the `ImportBatches` insert) rolls back — so you'll never see a `Failed` row in the table; you'll see *no* row for that attempt at all. Check application logs for import crashes, not the `ImportBatches` table.
