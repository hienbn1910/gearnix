# Gearnix — Project Plan

> **Scope of this document.** This is the authoritative plan for Gearnix: *what* is built, the business rules,
> the data model, and the *order* of work. *How* code is written is defined in `Rule.md`.
>
> - A Vietnamese translation is kept in `Workflow.vi.md`. If the two differ, this file wins.
> - Plan changes require the project owner's approval and are recorded in §19.
> - Section numbers are referenced from `CLAUDE.md`, `Rule.md` and GitHub issues. Do not renumber them.
> - Items marked **[To be confirmed]** are open questions; they are collected in §20.

---

## 1. Project summary

**Gearnix** is an e-commerce system for consumer electronics (phones, laptops, PC components, accessories)
built around **multi-warehouse inventory management**. The storefront is deliberately minimal: its job is
to generate real orders that exercise the inventory logic.

| | |
|---|---|
| **Purpose** | Portfolio project for a **.NET backend intern** application |
| **Priorities** | Finished > correct > deployed > explainable > feature count |
| **Core promise** | *"Stock is never oversold, and every stock change is traceable."* |
| **Naming** | "Gearnix" (repository `gearnix`) is used only for this project. All brands and products in seed data are fictional; no names, logos, images or UI are copied from real stores. |

---

## 2. Goals and non-goals

### 2.1 Core problems (required)

| ID | Problem | Definition of done |
|---|---|---|
| **P1** | High contention on scarce stock | 1,000 concurrent checkouts for 1 unit → exactly 1 order succeeds; stock never goes negative |
| **P2** | Reservations with expiry | Stock reserved by an unpaid order is released automatically after 15 minutes |
| **P3** | Idempotency | A double-clicked "Place order" creates one order; a duplicated payment webhook is applied once |
| **P4** | Stock ledger | Every `OnHand` change has an append-only ledger entry; the ledger sum always equals `OnHand` |
| **P5** | Server-side pricing and access control | Prices are computed on the server and snapshotted into the order; no user can read another user's order (IDOR) |
| **P6** | Vietnamese search | `dien thoai` matches `Điện thoại`; filters by category, brand and price range |
| **P7** | Traffic spikes (basic) | Catalog caching and rate limiting, with before/after numbers measured by k6 |

### 2.2 Bonus (only after every core task is done)

| ID | Feature |
|---|---|
| **B1** | AI assistant that answers "is X in stock?" from live inventory data and returns product links |

### 2.3 Non-goals

The following are out of scope unless the project owner explicitly requests them:

Redis, RabbitMQ, the Outbox pattern, microservices, flash-sale queues, vouchers/coupons, combos/gifts,
serial/IMEI tracking, splitting an order across warehouses, warehouse transfers, returns/RMA,
multi-channel sales (marketplaces, POS), a real payment gateway, a real shipping provider, reviews, wishlists,
recommendations, product comparison, a PC-build compatibility checker, trade-ins, installments,
e-invoices, email sending, Kubernetes.

> These are excluded on purpose. The goal is to be able to *explain* how each would be added (§18), not to build it.

---

## 3. Users and roles

| Role | Permissions |
|---|---|
| **Anonymous** | Browse and search products; see stock status (In stock / Low stock / Out of stock) |
| **Customer** | Anonymous permissions, plus: cart, checkout, mock payment, view and cancel **own** orders |
| **WarehouseStaff** | View stock and the ledger, create goods receipts, view orders, ship paid orders |
| **Admin** | WarehouseStaff permissions **except shipping and completing orders**, plus: manage categories, products, SKUs, prices, warehouses and stock adjustments |

- Each account has exactly one role.
- Customers self-register. WarehouseStaff and Admin accounts are created only by seed data.
- **Separation of duties:** only WarehouseStaff ship and complete orders. Admin can adjust stock; if the same role could
  also ship, one person could move goods out and correct the numbers afterwards. Keeping the two apart means every
  `Shipment` movement is created by warehouse staff.

---

## 4. Technology stack

| Area | Technology | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS), C# | |
| API | ASP.NET Core (controllers) | Thin controllers; see `Rule.md` §2 |
| Data access | EF Core 10 + Npgsql | `ExecuteUpdateAsync` for atomic stock updates |
| EF Core tooling | Microsoft.EntityFrameworkCore.Design + `dotnet-ef` tool | Migrations and the migration bundle |
| Database naming | EFCore.NamingConventions | snake_case names (§6); community package — confirm EF Core 10 support when installing |
| Database | PostgreSQL 17+ | `pg_trgm` extension for search |
| Validation | FluentValidation | Backend validation is mandatory |
| Authentication | ASP.NET Core Identity, cookie authentication | HttpOnly, Secure, SameSite cookies; no JWT in browser storage |
| Identity & key storage | Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | Users/roles and Data Protection keys stored in PostgreSQL (§6.5) |
| Errors | ProblemDetails | One error shape for every endpoint |
| API documentation | Microsoft.AspNetCore.OpenApi + Scalar | |
| Caching | HybridCache (in-memory, single instance) | No Redis (non-goal) |
| Rate limiting | ASP.NET Core built-in rate limiter | |
| Background jobs | `BackgroundService` + `PeriodicTimer` | Reservation expiry |
| Frontend | React + TypeScript + Vite | Two apps: `storefront` and `admin` |
| Frontend libraries | React Router, TanStack Query, Tailwind CSS, Zod | Zod for forms only |
| API types | openapi-typescript | Generated from the backend OpenAPI document |
| Tests | xUnit, Testcontainers.PostgreSql | Integration tests run against a real PostgreSQL |
| Seed data | Bogus | Referenced by the API project; the seeder runs only in `Development`, `Demo` and `LoadTest` (§13.4) |
| Load tests | k6 | |
| Local environment | Docker Compose | |
| CI | GitHub Actions | Build, test and lint on every pull request into `main` and every push to `main` |
| Hosting | VPS + Docker Compose + Caddy (HTTPS) | Provider chosen at W5-03 (§11.6) |
| AI (bonus) | Microsoft.Extensions.AI | Ollama locally; a hosted model for the demo |
| Task tracking | GitHub Issues + milestones, `gh` CLI | Specs live in issues (§17) |
| Agent workflow | Claude Code + mattpocock/skills | Process defined in `Rule.md` §1 |

Libraries outside this table require the project owner's approval (`Rule.md` §7).

---

## 5. Architecture

### 5.1 Style

Gearnix is a **modular monolith**: one ASP.NET Core project (`src/Api`), one PostgreSQL database and one `AppDbContext`,
with code organized into modules. The folder layout is defined in `Rule.md` §2.

```
 Browser (storefront)   Browser (admin)
          \                  /
           \   HTTPS /api   /
            v              v
        ┌───────────────────────────┐
        │      ASP.NET Core API      │
        │  Identity  Catalog         │
        │  Inventory Ordering Payment│
        │  Background job (expiry)   │
        └─────────────┬─────────────┘
                      │
                 PostgreSQL
```

In production, Caddy serves both React apps and proxies `/api` to the API on the **same domain**, so cookies work
without any cross-site configuration. In development, the Vite dev-server proxy plays the same role.

### 5.2 Modules

| Module | Owns | Public service for other modules |
|---|---|---|
| **Identity** | Users, roles, sign-in | `ICurrentUser` (id, role) |
| **Catalog** | Categories, products, SKUs, prices, search | `ICatalogService.GetSkusForCheckoutAsync(ids)` |
| **Inventory** | Warehouses, stock items, stock ledger, reservations | `IInventoryService`: `TryReserveAsync`, `ReleaseAsync`, `CommitAsync`, `ShipAsync`, `GetStockStatusAsync` |
| **Ordering** | Carts, orders, order lines, order status machine | `IOrderingService`: `MarkPaidAsync`, `GetOrderForPaymentAsync` |
| **Payment** | Payments, webhook handling, mock gateway | — |

### 5.3 Dependency rules

```
Ordering ──► Catalog     (prices, SKU data)
Ordering ──► Inventory   (reserve / release / ship)
Payment  ──► Ordering    (mark order paid)
```

- **No cycles.** Inventory never depends on Ordering; it stores only a reference (`ReferenceType`, `ReferenceId`).
- Modules call each other only through the public service interfaces above (`Rule.md` §2).
- **Transactions may span modules.** Because all modules share one `AppDbContext`, a service can open a transaction
  and call another module's service inside it — for example, Ordering creates the order while Inventory reserves
  stock, in **one** transaction. The service that opens a transaction owns it.
- **No EF navigation properties across modules.** Entities reference other modules' data by id only
  (`OrderLine.SkuId`, not `OrderLine.Sku`), which keeps module boundaries real.
- **The mock payment gateway behaves like an external system.** It talks to the Payment module only over HTTP
  (the webhook) and never calls services or the database directly.

---

## 6. Data model

Conventions for every table (columns implied by these conventions are not repeated below):

- **Primary keys:** `Guid` generated with `Guid.CreateVersion7()` (time-sortable, hard to guess).
- **Times:** `timestamptz`, always UTC.
- **Money:** `numeric(18,2)` ↔ C# `decimal`. VND is the only currency.
- **Audit columns:** every table has `CreatedAt`; mutable tables also have `UpdatedAt`.
- **Naming:** snake_case in the database (via an EF Core naming convention), PascalCase in C#.

### 6.1 Catalog

**categories**

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| Name | text | e.g. "Điện thoại" |
| Slug | text | Unique, e.g. `dien-thoai` |
| ParentId | uuid? | FK → categories; at most 2 levels |
| SortOrder | int | |

**products**

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| CategoryId | uuid | FK |
| Name | text | e.g. "Điện thoại Nova X 5G" |
| NameNormalized | text | Lowercase, diacritics removed, `đ`→`d`; set in C# on save; GIN trigram index |
| Slug | text | Unique |
| Brand | text | Fictional brand |
| Description | text | |
| Specs | jsonb | Category-specific specs, e.g. `{"cpu": "...", "ramGb": 16}` |
| MinPrice | numeric(18,2) | Lowest price among active SKUs; updated in the same transaction as any SKU change; used for price filtering and sorting |
| IsActive | bool | Inactive products are hidden from the storefront |

**skus** (sellable variants)

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| ProductId | uuid | FK |
| SkuCode | text | Unique, e.g. `NOVAX-128-BLK` |
| VariantName | text | e.g. "128GB - Đen" |
| Price | numeric(18,2) | Current price; must be > 0 |
| Attributes | jsonb | e.g. `{"storage": "128GB", "color": "Đen"}` |
| IsActive | bool | |

### 6.2 Inventory

**warehouses**

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| Code | text | Unique, e.g. `HN01`, `HCM01`, `DN01` |
| Name | text | |
| City | text | Used for warehouse selection (§7.4) |
| IsActive | bool | |

**stock_items** — one row per (SKU, warehouse)

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| SkuId | uuid | FK |
| WarehouseId | uuid | FK |
| OnHand | int | Units physically in the warehouse |
| Reserved | int | Units held for unshipped orders |

- Constraints: `UNIQUE (SkuId, WarehouseId)`, `CHECK (OnHand >= 0)`, `CHECK (Reserved >= 0)`, `CHECK (Reserved <= OnHand)`.
- `Available = OnHand - Reserved` is **computed, never stored**.
- The CHECK constraints are a last line of defense. Normal application flow must never rely on them.

**stock_movements** — the stock ledger (append-only: no `UPDATE`, no `DELETE`)

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| SkuId, WarehouseId | uuid | |
| Type | text | `Receipt`, `Shipment`, `Adjustment` |
| QuantityDelta | int | Change to `OnHand`; positive or negative, never 0 |
| OnHandAfter | int | `OnHand` immediately after this movement |
| ReferenceType | text? | e.g. `Order`, `GoodsReceipt` |
| ReferenceId | uuid? | |
| Reason | text? | Required for `Adjustment` |
| CreatedBy | uuid | Id of the acting user |
| CreatedAt | timestamptz | |

- Index: `(SkuId, WarehouseId, CreatedAt, Id)`.
- **Invariant:** for every stock item, `SUM(QuantityDelta) == OnHand`.
- Reservations do **not** create stock movements: they change `Reserved`, not `OnHand`.

**stock_reservations**

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderId | uuid | Reference only; no FK to Ordering tables |
| SkuId, WarehouseId | uuid | |
| Quantity | int | Must be > 0 |
| Status | text | `Active`, `Committed`, `Released`, `Shipped` |

The payment deadline is stored on the order (`orders.ExpiresAt`), not on the reservation.

### 6.3 Ordering

- **carts:** `Id`, `CustomerId` (unique), `UpdatedAt`
- **cart_items:** `CartId`, `SkuId`, `Quantity` (1–5); PK `(CartId, SkuId)`

**orders**

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderNumber | text | Unique and human-readable, e.g. `GN-20261121-000123` |
| CustomerId | uuid | Owner |
| Status | text | See §7.3 |
| WarehouseId | uuid | Fulfilling warehouse |
| Subtotal, Total | numeric(18,2) | `Total = Subtotal` (no shipping fees or discounts in scope) |
| IdempotencyKey | text | `UNIQUE (CustomerId, IdempotencyKey)` |
| RequestHash | text | SHA-256 of the normalized checkout request (§7.7) |
| ShippingName, ShippingPhone, ShippingAddress, ShippingCity | text | |
| ExpiresAt | timestamptz | Payment deadline |
| PaidAt, ShippedAt, CompletedAt, CancelledAt, ExpiredAt | timestamptz? | Set when the order enters that status |

`Status` is the single source of truth for why an order ended: `Cancelled` (by the customer) or `Expired`
(payment deadline passed). There is no separate cancel-reason column, so the two can never disagree.

**order_lines**

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderId | uuid | FK |
| SkuId | uuid | |
| ProductNameSnapshot, VariantNameSnapshot, SkuCodeSnapshot | text | Copied at checkout |
| UnitPrice | numeric(18,2) | Copied at checkout |
| Quantity | int | |
| LineTotal | numeric(18,2) | `UnitPrice * Quantity` |

### 6.4 Payment

**payments**

| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderId | uuid | |
| Provider | text | `Mock` |
| ProviderTransactionId | text? | **Unique**; set from the first webhook for this payment; used to deduplicate webhooks |
| Amount | numeric(18,2) | |
| Status | text | `Pending`, `Succeeded`, `Failed`, `RefundRequired`, `Refunded` |
| CreatedAt, UpdatedAt | timestamptz | |

### 6.5 Identity

- The standard ASP.NET Core Identity tables (users, roles, user roles), with extra user fields `FullName`, `Phone`, `City`.
- `data_protection_keys`: ASP.NET Core Data Protection keys, which encrypt authentication cookies. They are stored in the
  database so that redeploying the container does not sign every user out (§11.5).

### 6.6 Indexes, sequences and generated values

| Table | Index / constraint | Used by |
|---|---|---|
| products | unique `Slug`; `(CategoryId)`; `(MinPrice)`; GIN trigram on `NameNormalized` | Catalog list, search |
| skus | unique `SkuCode`; `(ProductId)` | Product detail, checkout |
| stock_items | unique `(SkuId, WarehouseId)`; `(WarehouseId)` | Reservation, stock overview |
| stock_movements | `(SkuId, WarehouseId, CreatedAt, Id)` | Ledger page (keyset pagination) |
| stock_reservations | `(OrderId)` | Release / commit / ship |
| orders | unique `OrderNumber`; unique `(CustomerId, IdempotencyKey)`; `(CustomerId, CreatedAt DESC)`; `(Status, CreatedAt)`; partial `(ExpiresAt) WHERE Status = 'PendingPayment'` | My orders, admin list, **expiry job** |
| order_lines | `(OrderId)` | Order detail |
| payments | unique `ProviderTransactionId`; `(OrderId)` | Webhook deduplication |

- **Order number:** generated from the PostgreSQL sequence `order_number_seq`, which is safe under concurrency.
  Format `GN-{yyyyMMdd}-{seq:D6}`, with the date in Vietnam time (`Asia/Ho_Chi_Minh`).
  The sequence never resets; gaps are allowed.
- **Clock:** the application's `TimeProvider` is the only source of time — never the database's `now()` — so tests can control time.

---

## 7. Business rules

### 7.1 Stock quantities

- `OnHand` — units physically in the warehouse.
- `Reserved` — units promised to orders that have not shipped yet.
- `Available = OnHand - Reserved` — units that can still be sold.

Customers never see exact quantities. They see a **stock status**:

| Status | Total available across active warehouses |
|---|---|
| `InStock` | > 5 |
| `LowStock` | 1–5 |
| `OutOfStock` | 0 |

WarehouseStaff and Admin see exact quantities per warehouse.

Stock status aggregates all warehouses, but each order is fulfilled from **one** warehouse (§7.4).
Checkout can therefore return `409` even when the product page shows "In stock"; the UI must handle this case.

### 7.2 Reserving stock (the most important rule)

Reserving `q` units of a SKU in a warehouse is **a single atomic conditional SQL update**:

```csharp
var affected = await db.StockItems
    .Where(s => s.SkuId == skuId
             && s.WarehouseId == warehouseId
             && s.OnHand - s.Reserved >= quantity)
    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Reserved, x => x.Reserved + quantity), ct);
// affected == 1 → reserved; affected == 0 → insufficient stock
```

- Never read stock, check it in C#, and then save.
- A multi-line order reserves every line **inside one transaction**. If any line fails, the whole transaction rolls back.
- Lines are reserved **in `SkuId` order** so that row locks are always taken in the same order, which prevents deadlocks.

### 7.3 Order lifecycle

```
                ┌──────────► Cancelled (customer cancels)
                │
PendingPayment ─┼──────────► Expired   (payment deadline passed)
                │
                └─► Paid ──► Shipped ──► Completed
```

| Transition | Triggered by | Stock effect |
|---|---|---|
| (new) → `PendingPayment` | Customer checkout | `Reserved += q`; reservation `Active` |
| `PendingPayment` → `Paid` | Payment webhook | Reservation `Committed` (still reserved, no longer expires) |
| `PendingPayment` → `Cancelled` | Customer | `Reserved -= q`; reservation `Released` |
| `PendingPayment` → `Expired` | Expiry job | `Reserved -= q`; reservation `Released` |
| `Paid` → `Shipped` | WarehouseStaff | `OnHand -= q`, `Reserved -= q`; `Shipment` movement; reservation `Shipped` |
| `Shipped` → `Completed` | WarehouseStaff (simulated delivery) | None |

- Every other transition is rejected with `409 Conflict`.
- A status change is a conditional update on the current status (`... WHERE Id = @id AND Status = 'PendingPayment'`),
  so two competing actions — for example, payment and expiry — can never both succeed.
- Cancelling a `Paid` order is out of scope.

### 7.4 Warehouse selection (no order splitting)

1. Build the candidate list: active warehouses, with `City == ShippingCity` first, then ordered by `Code`.
2. For each candidate, try to reserve **all** order lines in one transaction.
3. The first warehouse that succeeds fulfils the entire order.
4. If no warehouse can fulfil the entire order, return `409 Conflict` listing the SKUs that are short.

### 7.5 Pricing

- At checkout the server reads current SKU prices. Any price sent by the client is ignored.
- Price, product name, variant name and SKU code are copied into `order_lines` (the **snapshot**).
  Later catalog changes do not affect existing orders.
- `LineTotal = UnitPrice * Quantity`; `Total = Σ LineTotal`. VND has no minor unit in practice, but `decimal` is still used.
- Inactive products and SKUs cannot be ordered.

### 7.6 Cart

- One cart per customer; 1–5 units per line.
- Adding to the cart does **not** reserve stock.
- The cart shows current prices and stock status; checkout re-validates everything.
- After a successful checkout, the ordered items are removed from the cart **in the same transaction**.
- **A customer may have at most 3 `PendingPayment` orders.** A fourth checkout returns `409 too_many_pending_orders`.
  Without this limit, one user could lock up all stock with unpaid orders.

### 7.7 Checkout idempotency

- `POST /api/orders` requires an `Idempotency-Key` header: a client-generated UUID, one per checkout attempt.
- The database enforces `UNIQUE (CustomerId, IdempotencyKey)`.
- `RequestHash` is the SHA-256 of the normalized request (cart lines sorted by `SkuId`, plus shipping details).

| Situation | Result |
|---|---|
| Same key, same hash | Return the **existing** order with the same response (`200`); no new order |
| Same key, different hash | `409 idempotency_key_reused` |
| Previous attempt failed (e.g. out of stock) | Nothing was stored, so a retry with the same key runs again |
| Two concurrent requests with the same key | One insert succeeds; the other hits a unique violation |

In PostgreSQL a unique violation **aborts the current transaction**. Roll it back, then load the existing order in a new query.

### 7.8 Reservation expiry

- Payment deadline: `ExpiresAt = CreatedAt + 15 minutes`, configurable through `Ordering:PaymentTimeoutMinutes`.
- A `BackgroundService` runs every 60 seconds and processes expired `PendingPayment` orders in batches of 100.
- For each order, it performs a conditional status update to `Expired`; **only if that update succeeds** are the reservations released.
- Per-order conditional updates keep the job safe if it ever runs on several instances.
- The job stops cleanly on shutdown (`CancellationToken`). Each order is processed in its own transaction,
  so an interrupted batch leaves no partial state.
- Order responses include both `ExpiresAt` and `ServerNow`. The frontend countdown uses their difference,
  so an incorrect clock on the customer's device has no effect.

### 7.9 Payment (mock)

**Flow**

1. The customer starts a payment → a `Pending` payment is created and its id is passed to the mock gateway as the
   merchant reference → the customer is redirected to the mock payment page.
2. The mock gateway sends a webhook to `POST /api/payments/webhook`:
   - Body: `paymentId` (the merchant reference, echoed back), `providerTransactionId`, `orderId`, `amount`,
     `status` (`Succeeded` or `Failed`).
   - `X-Timestamp` header: Unix time in seconds when the webhook was sent.
   - `X-Signature` header: HMAC-SHA256 of `"{timestamp}.{rawBody}"`, using a shared secret from configuration/secrets.
3. The storefront learns the result by **polling** `GET /api/orders/{id}` every 2 seconds for up to 60 seconds
   (WebSocket/SignalR is out of scope).

**Mock gateway delivery.** Webhooks are sent from a **background worker** backed by an in-memory `Channel`, never from
the request thread. A non-2xx response is retried up to 3 times (after 1 s, 5 s and 25 s), as real gateways do —
which is exactly why the webhook handler must be idempotent.

**Webhook handling**

1. Verify the signature with a constant-time comparison and reject timestamps older than 5 minutes (replay protection)
   → `401`, logged as a warning.
2. Deduplicate by `ProviderTransactionId` (unique). If it was already processed → `200`, no further action.
3. Load the payment by `paymentId`. It must exist and belong to `orderId`; otherwise → `400`, logged as a warning.
   Set its `ProviderTransactionId`.
4. The amount must equal the order total. Otherwise → payment `Failed`, logged as a warning.
5. `Succeeded` and the order is still `PendingPayment` → order `Paid`, reservations `Committed`.
6. `Succeeded` but the order is already `Expired` or `Cancelled` → payment `RefundRequired`, then a mock refund → `Refunded`.
7. `Failed` → payment `Failed`. The order stays `PendingPayment` until it expires, and the customer may retry.

A customer who retries after a failed payment creates a new payment row, so one order can have several payments.
The echoed `paymentId` identifies the right row without guessing (for example, "the latest pending payment" would be
wrong when two attempts overlap). Real gateways work the same way with a merchant reference.

**Mock gateway endpoint.** `/api/mock-gateway/...` exists **only** in the `Development` and `Demo` environments.
It can simulate success, failure, a duplicate webhook (sent twice) and a delayed webhook.
The caller must own the payment: a customer cannot pay for someone else's order, even in Demo.

### 7.10 Inventory operations

- **Goods receipt** (WarehouseStaff, Admin): adds quantities for one or more SKUs to a warehouse.
  For each line, `OnHand += q` and one `Receipt` movement are written, all in one transaction.
- **Adjustment** (Admin only): a positive or negative correction with a required reason (e.g. stock count, damage).
  A negative adjustment may not make `OnHand < Reserved` → `409 adjustment_below_reserved`.
- **Shipment** (WarehouseStaff): allowed only for `Paid` orders; see §7.3.

### 7.11 Access control

- Customer endpoints always filter by the current user id taken from the authentication cookie.
- Requesting another customer's order returns `404`, not `403`, so the response does not reveal that the order exists.
- WarehouseStaff and Admin endpoints live under `/api/admin/...` and are protected by role-based policies.

---

## 8. Key flows

### 8.1 Checkout — `POST /api/orders`

**Before the transaction.** Keep the transaction short: reserved stock rows stay locked until commit.

1. Require an authenticated Customer, the CSRF header (§11.5) and an `Idempotency-Key`.
2. Validate input (shipping details, at least one cart line) and compute `RequestHash`.
3. If this customer already has an order with this key: same hash → return it; different hash → `409 idempotency_key_reused`.
4. If the customer already has 3 `PendingPayment` orders → `409 too_many_pending_orders`.
5. Load the cart lines and the SKUs from Catalog (active only, current prices). Compute line totals and the order total.
6. Build the candidate warehouse list (§7.4).

**One transaction per candidate warehouse.**

7. Reserve every line in `SkuId` order (§7.2). If any line fails → roll back and try the next warehouse.
8. Get the next order number; insert the order (`PendingPayment`, `ExpiresAt`), its order lines (snapshots) and its reservations (`Active`).
9. Remove the ordered items from the cart.
10. Commit. On a deadlock or serialization failure, retry the whole transaction (§11.3).

**After the transaction.**

11. Return `201` with the order id, order number, total, `ExpiresAt` and `ServerNow`.
12. Errors: no warehouse can fulfil the order → `409 out_of_stock` with the short SKUs; invalid input → `400`; not signed in → `401`.

### 8.2 Payment webhook — `POST /api/payments/webhook`

Anonymous endpoint protected by its signature; the steps are in §7.9. It must respond quickly and
make no external calls inside the transaction.

### 8.3 Expiry job

Every 60 seconds: select up to 100 order ids where `Status = PendingPayment AND ExpiresAt < now`.
For each order, in its own transaction: conditional status update → if exactly one row changed, release its reservations
(`Reserved -= q`, reservation `Released`). Log the number of expired orders at `Information` level.

### 8.4 Goods receipt — `POST /api/admin/stock/receipts`

Validate the lines → transaction { for each line: upsert the stock item, `OnHand += q`, insert a `Receipt` movement with `OnHandAfter` } → `201`.

### 8.5 Shipment — `POST /api/admin/orders/{id}/ship`

Transaction { conditional status update `Paid → Shipped` → for each reservation: `OnHand -= q`, `Reserved -= q`,
insert a `Shipment` movement, set the reservation to `Shipped` } → `200`.

### 8.6 Customer cancellation — `POST /api/orders/{id}/cancel`

Owner only. Transaction { conditional update `PendingPayment → Cancelled` → release reservations } → `200`.
Any other status → `409`.

---

## 9. API endpoints

All routes are prefixed with `/api`. Every list endpoint is paginated (`Rule.md` §3).

| Method | Route | Role | Notes |
|---|---|---|---|
| POST | `/auth/register` | Anonymous | Rate limited |
| POST | `/auth/login` | Anonymous | Rate limited |
| POST | `/auth/logout` | Any signed-in user | |
| GET | `/auth/me` | Any signed-in user | Id, name, role |
| GET | `/categories` | Anonymous | Cached |
| GET | `/products` | Anonymous | `q`, `categoryId`, `brand`, `minPrice`, `maxPrice`, `sort`, `page`, `pageSize`; cached |
| GET | `/products/{slug}` | Anonymous | Product and its SKUs; cached 60 s |
| GET | `/products/{slug}/stock-status` | Anonymous | Stock status per SKU; cached ≤ 10 s |
| GET | `/cart` | Customer | |
| PUT | `/cart/items/{skuId}` | Customer | Body `{ quantity }` |
| DELETE | `/cart/items/{skuId}` | Customer | |
| POST | `/orders` | Customer | `Idempotency-Key` header; rate limited |
| GET | `/orders` | Customer | Own orders only |
| GET | `/orders/{id}` | Customer | Own order only; otherwise `404` |
| POST | `/orders/{id}/cancel` | Customer | Own order, `PendingPayment` only |
| POST | `/orders/{id}/payments` | Customer | Creates a mock payment; returns the payment page URL |
| POST | `/payments/webhook` | Anonymous + signature | |
| POST | `/mock-gateway/payments/{paymentId}/simulate` | Development/Demo only | `{ outcome, duplicate, delaySeconds }` |
| GET/POST/PUT | `/admin/categories[...]` | Admin | |
| GET/POST/PUT | `/admin/products[...]`, `/admin/products/{id}/skus[...]` | Admin | Invalidates the catalog cache |
| GET/POST/PUT | `/admin/warehouses[...]` | Admin (GET also WarehouseStaff) | |
| GET | `/admin/stock` | WarehouseStaff, Admin | Filter by warehouse, SKU, low stock |
| POST | `/admin/stock/receipts` | WarehouseStaff, Admin | |
| POST | `/admin/stock/adjustments` | Admin | Reason required |
| GET | `/admin/stock/movements` | WarehouseStaff, Admin | Keyset pagination (`cursor`) |
| GET | `/admin/orders` | WarehouseStaff, Admin | Filter by status |
| POST | `/admin/orders/{id}/ship` | WarehouseStaff | `Paid` only; not Admin (separation of duties, §3) |
| POST | `/admin/orders/{id}/complete` | WarehouseStaff | `Shipped` only; not Admin (§3) |
| GET | `/health/live` | Anonymous | Process is running (Docker healthcheck) |
| GET | `/health/ready` | Anonymous | Database is reachable (uptime monitor, deploy check) |
| POST | `/assistant/chat` | Customer | **Bonus B1**; rate limited; behind a feature flag |

**Status codes**

| Code | Meaning |
|---|---|
| `200` / `201` | Success |
| `400` | Validation error |
| `401` | Not signed in |
| `403` | Wrong role (or missing CSRF header, §11.5) |
| `404` | Not found, or not owned by the caller |
| `409` | Business conflict (out of stock, invalid status) |
| `429` | Rate limited |

Every error is a ProblemDetails response with a stable `code` and the request's `traceId`.
Business error codes: `out_of_stock`, `invalid_status`, `too_many_pending_orders`, `idempotency_key_reused`,
`adjustment_below_reserved` (§7.10).

**Response shapes**

- Page-based lists: `{ items, page, pageSize, totalCount }` (default `pageSize` 20, maximum 100).
- Keyset lists: `{ items, nextCursor }` (`nextCursor` is `null` on the last page).
- No API versioning: there is a single client that we control. A breaking change updates the generated frontend types in the same pull request.

---

## 10. Search design (P6)

- `NameNormalized` is computed in C# whenever a product is saved: lowercase → remove diacritics
  (Unicode normalization FormD, then drop non-spacing marks) → replace `đ` with `d` → collapse whitespace.
- The same function normalizes the user's query.
- Query: `NameNormalized ILIKE '%term%'` (or the `%` similarity operator), backed by a **GIN trigram index** (`pg_trgm`).
- Filters: category (including sub-categories), brand, price range (lowest SKU price).
  Sort options: relevance, price ascending/descending, newest.
- The normalization function has unit tests with Vietnamese cases (`Điện thoại` → `dien thoai`, `Đồng hồ` → `dong ho`).

---

## 11. Non-functional design

### 11.1 Targets and capacity

These are **targets to verify, not measured results**. After the first k6 run, adjust them and record the change in §19.

| Area | Target |
|---|---|
| Deployment | 1 VPS (2 vCPU / 4 GB RAM), 1 API instance, 1 PostgreSQL instance |
| Catalog reads | p95 < 200 ms at 200 requests/s (with cache) |
| Checkout | p95 < 500 ms at 50 requests/s, normal stock levels |
| Flash buy (L2) | 1,000 concurrent buyers for 1 unit → 1 success, no errors other than `409`, all responses within 10 s |
| Correctness | 0 oversold units and the ledger invariant holds, in every test |
| Availability | Single server with no high availability (accepted for a demo); containers restart automatically |
| Recovery point objective (RPO) | Up to 24 h of data loss (daily backup) |
| Recovery time objective (RTO) | < 1 h (restore the latest backup) |

**Back-of-envelope estimates**

- 100,000 orders × ~2 lines ≈ 300,000 rows, roughly 100 MB including indexes. The ledger grows at a similar rate.
  One PostgreSQL instance is ample; no sharding or partitioning is needed.
- Connections: API pool `Maximum Pool Size=50`; PostgreSQL `max_connections=100`
  (leaving headroom for migrations, backups and admin tools); pool wait `Timeout=15` seconds.
- 1,000 concurrent requests will therefore **queue for connections**. This is expected, and the 10-second target accounts for it.

### 11.2 Performance (P7)

- **Short transactions.** Validation, pricing and warehouse candidate selection happen **before** the checkout transaction
  opens (§8.1). A reserved stock row stays locked until commit, so every extra millisecond inside the transaction slows
  down every buyer of the same SKU.
- **Catalog caching** with HybridCache: categories, product list pages and product detail, with a 60-second expiry.
  Product, SKU and price changes remove the affected keys. Stock status has its own endpoint with a short cache (≤ 10 s).
  The product list does not show stock status, which keeps list caching simple.
- Cached stock is **never** used to decide whether a reservation succeeds.
- **Rate-limiting policies**

  | Policy | Limit |
  |---|---|
  | `auth` (login, register) | 5 requests/minute per IP |
  | `checkout` | 10 requests/minute per user |
  | `assistant` (bonus) | 20 requests/hour per user |
  | Global | 100 requests/10 seconds per IP |

  Rejected requests receive `429` with a `Retry-After` header.
- **Queries:** `AsNoTracking`, projection with `Select`, pagination, and indexes on filtered columns.
- **Load testing with k6** (scripts in `tests/load/`):
  - `L1-catalog-spike.js` uses k6's `ramping-arrival-rate` executor to ramp up to **200 requests/s** of catalog browsing
    and hold it for about 2 minutes, matching the §11.1 target; an optional stage at about 400 requests/s finds the limit.
    It runs **before** and **after** caching is added. The load is set in requests/s, not virtual users, because the target
    is defined in requests/s. Adjust the numbers after the first run (§11.1).
  - `L2-flash-buy.js` has 1,000 virtual users try to buy the same 1-unit SKU.
  - Record p95 latency, error rate and machine specs in `README.md`. Only real, measured numbers are published.
  - k6 runs against the `LoadTest` environment (§11.6): rate limiting is off (otherwise every k6 request shares one IP
    and gets `429`), and 1,000 load-test customers are seeded and signed in during k6's `setup()`.
  - k6 runs from a **different machine** than the server; running both on one machine skews the numbers.
  - The rate limiter is tested separately: one integration test and one small k6 script that expects `429`.

### 11.3 Reliability and failure modes

| Failure | Required behavior |
|---|---|
| API crashes mid-checkout | The transaction never committed, so PostgreSQL rolls it back: nothing is reserved and no order exists |
| Double click or client retry | Idempotency key (§7.7) |
| Webhook duplicated or retried | Deduplicated by `ProviderTransactionId` (§7.9) |
| Webhook never arrives | The order expires and stock is released; a late webhook leads to `RefundRequired` |
| Expiry job crashes or server restarts | All state is in the database; the next run continues |
| Shutdown during a job run | `CancellationToken` is honored; one transaction per order |
| Database unavailable | `/health/ready` reports unhealthy; the API returns `503` ProblemDetails; the app still starts (no database work at startup) |
| Deadlock or serialization failure (`40P01`, `40001`) | Retry the whole transaction up to 3 times with a short random delay (checkout, shipment, expiry) |
| Disk full | Prevented by log rotation and backup retention (§11.6) |
| Bad deployment | Redeploy the previous image tag (§11.6) |

**Database rules**

- Use PostgreSQL's default isolation level, `READ COMMITTED`; the atomic conditional update is correct under it.
- Do **not** enable EF Core's `EnableRetryOnFailure` globally — it conflicts with manually opened transactions.
  Retry explicitly in the places listed above.
- After any database error inside a transaction (e.g. a unique violation), the transaction is aborted:
  roll back, then run follow-up queries outside it.

### 11.4 Observability

- Structured logs are written to stdout (`Rule.md` §6) and collected by Docker.
- Every request has a trace id. It appears in every log line and in every ProblemDetails response as `traceId`,
  so an error reported by a user can be located in the logs.
- Business events are logged: order created / expired / paid / shipped, stock received / adjusted, webhook rejected.
- `/health/live` serves Docker's healthcheck (restart only if the process is stuck, not when the database is down);
  `/health/ready` serves monitoring and the deploy check.
- An external uptime monitor (any free service) checks `/health/ready` every 5 minutes and alerts the project owner.
- OpenTelemetry traces and metrics are optional, only if time allows (W5-02).

### 11.5 Security design

| Topic | Design |
|---|---|
| Auth cookie | HttpOnly, Secure, `SameSite=Strict`, 7-day sliding expiration |
| No login redirects | Cookie events return `401` / `403` instead of ASP.NET Core's default redirect to a login page |
| CSRF | Besides `SameSite=Strict`, every state-changing request (POST/PUT/PATCH/DELETE) must send `X-CSRF: 1`; otherwise `403`. Browsers cannot add custom headers to cross-site requests without a CORS preflight, which the API does not allow. The webhook is exempt because it is signed. |
| Data Protection keys | Stored in PostgreSQL (`data_protection_keys`) so redeploys do not invalidate cookies |
| Identity | Password ≥ 8 characters; 15-minute lockout after 5 failed sign-ins; the error message is always "email or password is incorrect" |
| Reverse proxy | Forwarded Headers middleware, trusting only the Caddy container, so the real client IP (for rate limiting and logs) and HTTPS scheme are used. Without it, all users would share Caddy's IP and a single rate limit. |
| Security headers (set in Caddy) | HSTS, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, a basic Content-Security-Policy |
| Webhook | Signature + timestamp + constant-time comparison (§7.9) |
| Secrets | user-secrets in development; on the VPS, environment variables from a `.env` file (permission 600, not in git) |
| Dependencies | CI runs `dotnet list package --vulnerable` and `npm audit --audit-level=high`; Dependabot is enabled |
| Public demo | Rate limits on; a spending limit set at the AI provider (bonus B1) |

### 11.6 Environments, deployment and operations

| Environment | Purpose | Differences |
|---|---|---|
| `Development` | Local work | Mock gateway on, demo seed data, user-secrets |
| `Test` | Integration tests | Testcontainers, test authentication scheme (§13.2) |
| `LoadTest` | k6 runs | Rate limiting off, 1,000 load-test customers seeded |
| `Demo` | Public deployment | Mock gateway on, demo seed data, 1-minute payment timeout |

- **Configuration** uses options classes validated at startup (`ValidateOnStart`), so missing configuration fails immediately.
- **CI** (every pull request into `main` and every push to `main`; feature-branch pushes are covered by their pull request): build, tests, lint, vulnerability checks.
- **Images:** on every merge to `main`, Docker images are built, tagged with the git commit SHA and pushed to GitHub Container Registry.
- **Deploy** (manual `workflow_dispatch`, over SSH): back up the database → `docker compose pull` → run the EF Core
  **migration bundle** → run the `seed` command (idempotent, §13.4) → `docker compose up -d` → wait for `/health/ready`.
- **Migrations** never run automatically at application startup. They must be backward compatible with the previous
  application version (add new columns as nullable first; drop old columns only in a later release), so rollback works.
- **Rollback:** redeploy the previous image tag. Restoring a backup is the last resort.
- **Backups:** daily `pg_dump`, kept for 7 days and copied off the VPS. **One restore test is performed before the demo.**
- PostgreSQL data lives in a named Docker volume. All containers use `restart: unless-stopped`.
- Docker log rotation: `max-size: 10m`, `max-file: 3`.
- **Hosting** (decided at W5-03): preferred option is a free VM (e.g. Oracle Cloud Always Free, sized 2 vCPU / 4 GB to
  match §11.1) with a free subdomain (e.g. DuckDNS); fallback is a low-cost VPS. A hostname is required either way:
  the auth cookie is `Secure` (§11.5), so the site must use HTTPS, and Caddy needs a hostname to obtain a certificate.

### 11.7 Scaling constraints (known and accepted)

The project assumes **a single API instance**. The table lists what would break with two or more instances, and the fix.

| Component | Problem with several instances | Fix (to explain, not to build) |
|---|---|---|
| In-memory cache | Invalidation on one instance does not reach the others | Redis as a shared cache, or a very short expiry |
| Rate limiter | Each instance counts separately | A Redis-backed limiter, or limiting at the proxy/gateway |
| Expiry job | Runs on every instance | Already safe (conditional updates); add `FOR UPDATE SKIP LOCKED` to avoid duplicate work |
| Data Protection keys | — | Already shared through the database |
| Mock gateway queue | In memory; lost on restart | Acceptable for a mock |

---

## 12. Frontend

### 12.1 Storefront (`src/web/storefront`)

| Page | Notes |
|---|---|
| Home / product list | Category menu, search box, filters, pagination (no stock status) |
| Product detail | SKU selector, price, stock status (separate request), add to cart |
| Cart | Quantities, current prices, stock warnings |
| Checkout | Shipping form (Zod); place order with an idempotency key; submit button disabled while pending; handles every `409` code |
| Mock payment page | Buttons: Success / Fail / Duplicate webhook / Delayed; then polls the order status |
| My orders, Order detail | Status, countdown from `ExpiresAt` − `ServerNow`, cancel button |
| Login / Register | |
| Chat widget | **Bonus B1** |

Every state-changing request sends the `X-CSRF: 1` header, set once in the shared API client.

### 12.2 Admin (`src/web/admin`)

| Page | Role |
|---|---|
| Products & SKUs | Admin |
| Categories | Admin |
| Warehouses | Admin |
| Stock overview (per warehouse, low-stock filter) | WarehouseStaff, Admin |
| Goods receipt form | WarehouseStaff, Admin |
| Stock adjustment form | Admin |
| Stock ledger (per SKU/warehouse) | WarehouseStaff, Admin |
| Orders list with Ship / Complete actions | WarehouseStaff, Admin |

UI rules: handle loading, empty and error states; show clear messages for `409` (out of stock, invalid status) and `429`.
Keep the design simple (Tailwind); no time is spent on animations.

---

## 13. Testing strategy

### 13.1 Unit tests

- Vietnamese text normalization
- Price and total calculation
- Order status transition rules
- Validators
- Webhook signature verification

### 13.2 Integration tests (Testcontainers + PostgreSQL)

Most tests use a **test authentication scheme** in which a request header selects the user and role, so parallel tests
do not pay for password hashing. A few tests use the real cookie sign-in to cover it.

| Area | Cases |
|---|---|
| Authentication | `401` when signed out (not a `302` redirect); `403` for the wrong role on admin endpoints; login lockout after 5 failed attempts |
| CSRF | POST without `X-CSRF` → `403` |
| IDOR | Customer B requesting customer A's order → `404` |
| Checkout | Success; out of stock → `409`; a client-sent price is ignored; response contains `ServerNow` |
| Idempotency | Same `Idempotency-Key` twice → one order, same response; same key with a different cart → `409 idempotency_key_reused` |
| Pending-order limit | A fourth unpaid order → `409 too_many_pending_orders` |
| Inventory | Goods receipt and adjustment write stock movements; a negative adjustment below `Reserved` → `409 adjustment_below_reserved`; shipment updates `OnHand`, `Reserved` and the ledger |
| Webhook | Bad signature → `401`; timestamp older than 5 minutes → `401`; amount mismatch → payment `Failed` |
| Errors | Error responses contain `traceId` |
| Search | `dien thoai` finds `Điện thoại` |

### 13.3 Concurrency tests (required; see `Rule.md` §9)

| ID | Scenario | Expected result |
|---|---|---|
| **C1** | 200 parallel checkouts (different customers) for a SKU with 1 unit | Exactly 1 success, 199 × `409`; `Reserved == 1`; `OnHand - Reserved >= 0` |
| **C2** | 50 parallel checkouts with the same `Idempotency-Key` | Exactly 1 order |
| **C3** | The same successful webhook sent 10 times in parallel | 1 payment applied; order becomes `Paid` once |
| **C4** | The expiry job and a successful webhook race on the same order | Exactly one of `Paid` / `Expired`; stock consistent; a late payment → `RefundRequired` |
| **C5** | Random mix of receipts, adjustments, checkouts, cancellations and shipments | For every stock item: `SUM(QuantityDelta) == OnHand` and `0 <= Reserved <= OnHand` |

C1 uses 200 requests so that it stays CI-friendly; the 1,000-user version runs in k6 (`L2-flash-buy.js`).

### 13.4 Seed data (Bogus, deterministic seed)

- 4 top-level categories — Phones, Laptops, PC components, Accessories — plus a few sub-categories.
- About 40 products and 120 SKUs, with fictional brands and Vietnamese product names with diacritics.
- 3 warehouses: `HN01` (Hà Nội), `HCM01` (TP.HCM), `DN01` (Đà Nẵng).
- **Initial stock is created through the goods-receipt service**, never by inserting `stock_items` directly,
  so the ledger invariant (`SUM(QuantityDelta) == OnHand`) holds from the start.
- Seeding is idempotent: running it twice changes nothing.
- `LoadTest` only: 1,000 customers, `load-0001@test.local` … `load-1000@test.local`.
- Demo accounts (Development and Demo only): `customer1@demo.local`, `customer2@demo.local`, `staff@demo.local`,
  `admin@demo.local`. Demo passwords are set through configuration and documented in the README.
- One SKU with exactly 1 unit, for the flash-buy demo.

**How seeding runs.** The API project has a `seed` command-line mode: started with it, the app seeds the database and
exits without starting the web server. It is run manually in development and as a deploy step after the migration
bundle (§11.6). This keeps startup free of database work (§11.3). The seeder runs inside the application, not as a
SQL script, because initial stock must go through the goods-receipt service.

| Task | Seeds |
|---|---|
| W1-05 | Roles and the `staff@demo.local` / `admin@demo.local` accounts (introduces the `seed` command) |
| W1-08 | Categories, products, SKUs and the demo customers |
| W2-03 | Initial stock, through the goods-receipt service |
| W4-07 | The 1,000 `LoadTest` customers |

---

## 14. Roadmap and task list

**Legend:** ⭐ marks a core task. The project owner should write it first, or at least understand it fully (`CLAUDE.md` §7).
Every task follows the full process in `Rule.md` §1.

**Tracking with GitHub Issues**

- This list is the **plan**. A task gets a **GitHub issue** (its spec), created by `/to-spec`, when work on it starts.
  Issues are not created in advance, so the plan and the issues cannot drift apart.
- When the issue exists, add its number after the task ID, e.g. `**W3-03** (#14) Checkout ...`.
- Each issue belongs to its week's GitHub milestone (`Week 1` … `Week 5`, `Bonus`).
- A task is done when its pull request (`Closes #14`) is merged. Then tick it here and in `Workflow.vi.md`.

**Current phase:** Week 1 — in progress

### Week 1 — Foundation and Catalog

- [x] **W1-01** (#6) Repository setup: solution, folders per `Rule.md` §2, `.editorconfig`, `Directory.Build.props`
  (nullable enabled, warnings as errors), `docker-compose.yml` (PostgreSQL), `.gitignore`, README skeleton.
  *Done when:* `docker compose up -d` followed by `dotnet run` starts the API.
- [x] **W1-02** (#8) CI with GitHub Actions: backend build and tests, .NET vulnerability check; Dependabot.
  Frontend jobs are added in W1-09, once the apps exist.
  *Done when:* a pull request shows green checks.
- [ ] **W1-03** (#10) Data foundation: `AppDbContext`, snake_case naming, UTC and decimal conventions, `TimeProvider`,
  connection pool settings, options validation at startup, first migration, `/health/live` and `/health/ready`.
  Also remove the CI allowance `--ignore-exit-code 8` once the first test exists (from W1-02, #8).
- [ ] **W1-04** Error handling and API docs: ProblemDetails with `code` and `traceId`, FluentValidation wiring,
  standard list response types (§9), OpenAPI + Scalar.
- [ ] **W1-05** Identity: register, login, logout, me; cookie settings (§11.5); `401`/`403` instead of redirects;
  CSRF header check; lockout; Data Protection keys in the database; the `seed` command with roles and the
  WarehouseStaff/Admin accounts (§13.4); authorization policies; test authentication scheme for integration tests.
  *Done when:* tests prove the `401`, `403`, missing-CSRF and lockout cases.
- [ ] **W1-06** Catalog administration: categories, products, SKUs (DTOs, validators, slug generation, `MinPrice` maintenance).
- [ ] **W1-07** Public catalog: product list (filters, pagination) and product detail.
- [ ] **W1-08** Seed data with Bogus (§13.4): categories, products, SKUs and demo customers. **No stock yet** (added in W2-03).
- [ ] **W1-09** Frontend setup: both Vite apps, routing, Tailwind, TanStack Query, API type generation,
  login page, storefront product list and detail, admin product management; add frontend lint, build and
  `npm audit` jobs to CI.
  *Done when:* CI checks both frontend apps.

### Week 2 — Inventory

- [ ] **W2-01** Warehouses (admin).
- [ ] **W2-02** `stock_items` table with CHECK constraints and the unique index.
- [ ] ⭐ **W2-03** Goods receipt with stock movements in the same transaction; extend the seed to create initial stock
  through this service.
- [ ] **W2-04** Stock adjustment (Admin only, reason required, cannot go below `Reserved`).
- [ ] **W2-05** Stock overview and ledger query (keyset pagination).
- [ ] **W2-06** Stock status endpoint (`InStock` / `LowStock` / `OutOfStock`) and its display on the product detail page.
- [ ] **W2-07** Admin UI: warehouses, stock overview, goods receipt form, adjustment form, ledger page.

### Week 3 — Cart and Checkout

- [ ] **W3-01** Cart API.
- [ ] ⭐ **W3-02** Inventory reservation service: `TryReserve`, `Release`, `Commit`, `Ship` (atomic updates, §7.2).
- [ ] ⭐ **W3-03** Checkout (§8.1): warehouse selection, price snapshot, order number sequence,
  order + lines + reservations + cart cleanup in one transaction, idempotency key and request hash,
  pending-order limit, retry on deadlock/serialization failure.
- [ ] **W3-04** My orders list and detail with ownership checks (IDOR tests).
- [ ] **W3-05** Customer cancellation.
- [ ] ⭐ **W3-06** Concurrency tests C1 and C2.
- [ ] **W3-07** Storefront: cart, checkout, my orders and order detail pages.

### Week 4 — Payment, expiry, search, performance

- [ ] **W4-01** Start payment + mock gateway (Development/Demo only, HTTP only, background sender with retries,
  ownership check) + mock payment page with status polling.
- [ ] ⭐ **W4-02** Payment webhook: timestamped signature, deduplication, amount check, mark paid; test C3.
- [ ] ⭐ **W4-03** Reservation expiry background job; late payment → `RefundRequired` → `Refunded`; test C4.
- [ ] **W4-04** Shipment and completion (WarehouseStaff); test C5.
- [ ] **W4-05** Vietnamese search: normalization, trigram index, filters.
- [ ] **W4-06** Catalog caching and rate-limiting policies.
- [ ] **W4-07** `LoadTest` environment with 1,000 load-test accounts; k6 scripts L1 and L2 run from a separate machine;
  before/after measurements compared with the §11.1 targets; results written to the README.

### Week 5 — Ship it

- [ ] **W5-01** UI polish: loading, empty and error states; messages for `409` and `429`.
- [ ] **W5-02** Observability review (§11.4): structured logs, masking, trace ids, business events, uptime monitor.
  OpenTelemetry only if time allows.
- [ ] **W5-03** Production Dockerfiles and compose file, Caddy (HTTPS, security headers), forwarded headers,
  images on GitHub Container Registry, manual deploy workflow with the migration bundle, daily backups
  **and one restore test**, log rotation, Demo seeding (§11.5, §11.6).
- [ ] **W5-04** Complete README (§16); confirm every ADR in §17 exists; screenshots or a short demo GIF.
- [ ] **W5-05** Rehearse the demo script (§15); final review of the whole codebase against `Rule.md`.

### Week 6 — Bonus B1: AI assistant (optional; only after Weeks 1–5)

- [ ] **B1-01** Read-only tools: search products, get stock status, get my order status.
- [ ] **B1-02** `/api/assistant/chat` with Microsoft.Extensions.AI, rate limit and feature flag.
- [ ] **B1-03** Storefront chat widget showing product cards (links built by the backend).
- [ ] **B1-04** Evaluation set: 30 questions with expected answers, runnable as a test.

**AI rules:** tools are read-only; the user id comes only from the authentication context; links, prices and stock status
come from backend data, never from the model; all text is treated as untrusted input (`Rule.md` §11).

**If time runs out:** stop after Week 4, then do W5-03 and W5-04 (deployment and README). The result is still a complete project.

---

## 15. Demo script (about 3 minutes)

1. Open the admin stock page: SKU `NOVAX-128-BLK` has 1 unit in `HCM01`.
2. Run `k6 run tests/load/L2-flash-buy.js` (1,000 buyers) → 1 success, 999 out-of-stock responses; stock never negative.
3. Place an order without paying → after the timeout (1 minute in Demo) the order is `Expired` and the stock is available again.
4. On the mock payment page, click "Duplicate webhook" → the order is paid once and there is one payment row.
5. WarehouseStaff ships the order → open the ledger: every number is explained by a stock movement.
6. Sign in as another customer and open the first customer's order URL → `404`.
7. Search for `dien thoai` → Vietnamese results.
8. (Bonus) Ask the assistant "Nova X 128GB màu đen còn hàng không?" → an answer with a product card and link.

---

## 16. README checklist

- [ ] One-paragraph description and the core promise
- [ ] Live demo link and demo accounts per role
- [ ] Architecture diagram and order/stock state diagram
- [ ] "Hard problems and how I solved them": P1–P7, 3–4 lines each
- [ ] Test results: concurrency tests and k6 numbers with machine specs
- [ ] How to run locally with one command
- [ ] Technology stack
- [ ] Known limitations and failure handling (§11.3, §11.7)
- [ ] What I would do next at larger scale (§18)

---

## 17. Documentation in the repository

| What | Where | Created by |
|---|---|---|
| Spec of each task | GitHub issue (template of the `to-spec` skill) | `/grill-with-docs` → `/to-spec` |
| Domain vocabulary | `GLOSSARY.md` | `/grill-with-docs` (domain modeling) |
| Design decisions | `docs/adr/NNNN-title.md` (context, decision, alternatives, consequences) | `/grill-with-docs`, or when the decision is made |
| Agent skill configuration | `docs/agents/*.md` | `/setup-matt-pocock-skills` (once) |

Every spec issue lists `Task: W?-??` and the relevant `Workflow.md` sections under "Further Notes".

ADRs to write (no later than when the related task is done):

1. `0001-atomic-conditional-update-for-stock.md` — compared with pessimistic locking and optimistic concurrency tokens
2. `0002-append-only-stock-ledger.md`
3. `0003-idempotency-key-for-checkout.md`
4. `0004-cookie-auth-instead-of-jwt-in-browser.md`
5. `0005-no-redis-or-message-queue-yet.md`
6. `0006-csrf-protection-for-cookie-auth.md`
7. `0007-single-instance-deployment-and-backups.md`
8. `0008-github-issues-and-agent-skills-workflow.md`

---

## 18. Interview talking points: how it would scale (explain, do not build)

| Question | Direction of the answer |
|---|---|
| 100× more traffic? | More stateless API instances, cache moved to Redis, read replicas for the catalog, a CDN for the frontend |
| A real flash sale? | A Redis counter as a gate in front of the database, a queue that processes orders sequentially, a cached "sold out" flag |
| Payment gateway down? | Retry with backoff, a circuit breaker, a reconciliation job that asks the gateway for missed payments |
| Reliable events (email, analytics)? | An outbox table written in the same transaction, plus a message broker (RabbitMQ) |
| An order needs items from 2 warehouses? | Split it into one shipment per warehouse, each with its own status |
| Why a modular monolith? | Simple to build, deploy and test; modules can be extracted later if a real need appears |

---

## 19. Plan change log

| Date | Change | Approved by |
|---|---|---|
| 2026-11-21 | Initial plan | Project owner |
| 2026-11-21 | Adopt mattpocock/skills: specs as GitHub issues (created per task), `GLOSSARY.md`, `docs/adr/`, test-first in vertical slices, independent `code-review`, project owner pushes and merges | Project owner |
| 2026-11-21 | System design review: targets and capacity, reliability, observability, security, deployment and operations (§11); indexes and order number sequence (§6.6); idempotency request hash, pending-order limit, webhook replay protection and retries, cart cleanup inside the checkout transaction; seed stock via goods receipts; stock status as a separate endpoint | Project owner |
| 2026-11-21 | Product renamed to Gearnix (repository `gearnix`); order number prefix `GN-` (previously logged as 2026-11-22, a time-zone mix-up) | Project owner |
| 2026-11-21 | Editorial rewrite of `Workflow.md` and `Workflow.vi.md`: wording, structure and tables only; no change to scope or rules; open questions marked **[To be confirmed]** and collected in §20 | Project owner |
| 2026-11-23 | Open questions resolved: only WarehouseStaff ship/complete (§3); approved packages added to §4; `CancelReason` replaced by `ExpiredAt` (§6.3); webhook carries `paymentId` (§7.9); error code `adjustment_below_reserved` (§9); L1 load set in requests/s (§11.2); hosting decided at W5-03 (§11.6); `seed` command and per-task seeding (§13.4, §11.6); frontend CI jobs move to W1-09 (§14) | Project owner |
| 2026-11-26 | CI triggers narrowed to pull requests into `main` and pushes to `main` (§4, §11.6); W1-03 removes the temporary CI allowance for "zero tests ran" (§14) | Project owner |

---

## 20. Open questions

When a question about the plan cannot be settled immediately, mark it **[To be confirmed]** where it appears and add
it here. Once resolved with the project owner, update the section, remove the marker and the row, and record the
decision in §19.

| # | Section | Question |
|---|---|---|
| — | — | No open questions (all resolved on 2026-11-23; see §19) |
