# Workflow.md — Gearnix Project Plan

> **What this file is:** the single source of truth for **WHAT** to build, the business rules, the data model,
> and the **ORDER** of work.
> **How to write the code** is defined in `Rule.md`.
> Vietnamese translation for the human: `Workflow.vi.md`. If the two differ, **this English file wins**.
> Changes to this plan must be approved by the human and recorded in section 19.

---

## 1. Project summary

**Gearnix** is an e-commerce system that sells electronics (phones, laptops, PC components, accessories)
and whose **core is multi-warehouse inventory management**.
The storefront is intentionally simple: its main job is to create real orders that exercise the inventory logic.

- **Purpose:** portfolio project for a **.NET backend intern** job application.
- **Priority order:** finished > correct > deployed > explainable > number of features.
- **Core promise (one sentence):** *"Stock is never oversold, and every stock change is traceable."*
- **Product name:** "Gearnix" (repository `gearnix`). The name is used only for this portfolio project. All brands and products in seed data
  are fictional. Never copy names, logos, images or UI of real stores.

---

## 2. Goals and non-goals

### 2.1 Core problems (must be done)

| ID | Problem | What "done" means |
|---|---|---|
| **P1** | Many buyers, little stock | 1,000 concurrent checkouts for 1 unit → exactly 1 order succeeds, stock never negative |
| **P2** | Reservation with expiry | Unpaid orders release their reserved stock after 15 minutes, automatically |
| **P3** | Idempotency | Double-clicking "Place order" creates 1 order; a duplicated payment webhook is applied once |
| **P4** | Stock ledger | Every change to `OnHand` has an append-only ledger row; ledger sum always equals `OnHand` |
| **P5** | Server-side pricing & access control | Prices computed on server and snapshotted into the order; a user can never see another user's order (IDOR) |
| **P6** | Vietnamese search | Typing `dien thoai` finds `Điện thoại`; filter by category, brand, price range |
| **P7** | Traffic spikes (basic) | Catalog caching + rate limiting, with before/after numbers measured by k6 |

### 2.2 Bonus (only after every core task is done)

| ID | Feature |
|---|---|
| **B1** | AI assistant: answers "is X in stock?" from real inventory data and returns product links |

### 2.3 Non-goals (do NOT build unless the human explicitly asks)

Redis, RabbitMQ, Outbox pattern, microservices, flash-sale queue, vouchers/coupons, combos/gifts,
serial/IMEI tracking, splitting an order across warehouses, warehouse transfers, returns/RMA,
multi-channel sales (marketplace, POS), real payment gateway, real shipping provider, reviews, wishlist,
recommendations, product comparison, PC-build compatibility checker, trade-in, installments,
e-invoices, email sending, Kubernetes.

> These are deliberately out of scope. The human should be able to *explain* how they would be added
> (see section 18), not build them.

---

## 3. Users and roles

| Role | Can do |
|---|---|
| **Anonymous** | Browse and search products, see stock status (In stock / Low stock / Out of stock) |
| **Customer** | Everything above + cart, checkout, pay (mock), view and cancel **own** orders |
| **WarehouseStaff** | View stock and ledger, create goods receipts, view orders, ship paid orders |
| **Admin** | Everything above + manage categories, products, SKUs, prices, warehouses, stock adjustments |

- One account has exactly one role.
- Customers register themselves. Staff and Admin accounts are created by seed data only.

---

## 4. Tech stack

| Area | Technology | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS), C# | |
| API | ASP.NET Core with controllers | Thin controllers, see `Rule.md` §2 |
| Data access | EF Core 10 + Npgsql | `ExecuteUpdateAsync` for atomic stock updates |
| Database | PostgreSQL 17 or newer | `pg_trgm` extension for search |
| Validation | FluentValidation | Backend validation is mandatory |
| Auth | ASP.NET Core Identity, cookie auth | HttpOnly, Secure, SameSite cookies; no JWT in browser storage |
| Errors | ProblemDetails | Same error shape everywhere |
| API docs | Microsoft.AspNetCore.OpenApi + Scalar | |
| Caching | HybridCache (in-memory, single instance) | No Redis (non-goal) |
| Rate limiting | Built-in ASP.NET Core rate limiter | |
| Background jobs | `BackgroundService` + `PeriodicTimer` | Reservation expiry |
| Frontend | React + TypeScript + Vite | Two apps: `storefront`, `admin` |
| Frontend libs | React Router, TanStack Query, Tailwind CSS, Zod | Zod only for forms |
| API types | openapi-typescript | Types generated from backend OpenAPI |
| Tests | xUnit, Testcontainers (PostgreSQL), Bogus | Integration tests use a real PostgreSQL |
| Load tests | k6 | |
| Local run | Docker Compose | |
| CI | GitHub Actions | Build + test + lint on every push/PR |
| Deploy | VPS + Docker Compose + Caddy (HTTPS) | Any equivalent cheap/free host is fine |
| AI (bonus) | Microsoft.Extensions.AI | Ollama locally, hosted model for demo |
| Task tracking | GitHub Issues + milestones, `gh` CLI | specs live in issues (§17) |
| Agent workflow | Claude Code + mattpocock/skills | process in `Rule.md` §1 |

Do not add libraries outside this table without the human's approval (see `Rule.md` §7).

---

## 5. Architecture

### 5.1 Style

**Modular monolith**: one ASP.NET Core project (`src/Api`), one PostgreSQL database, one `AppDbContext`,
code split into modules. Folder layout is defined in `Rule.md` §2.

```
 Browser (storefront)   Browser (admin)
          \                  /
           \   HTTPS /api   /
            v              v
        ┌───────────────────────────┐
        │        ASP.NET Core API    │
        │  Identity  Catalog         │
        │  Inventory Ordering Payment│
        │  Background job (expiry)   │
        └─────────────┬─────────────┘
                      │
                 PostgreSQL
```

In production, Caddy serves both React apps and proxies `/api` to the API on the **same domain**,
so cookies work without cross-site configuration. In development, Vite's dev-server proxy does the same.

### 5.2 Modules

| Module | Owns | Public service used by other modules |
|---|---|---|
| **Identity** | Users, roles, login | `ICurrentUser` (id, role) |
| **Catalog** | Categories, products, SKUs, prices, search | `ICatalogService.GetSkusForCheckoutAsync(ids)` |
| **Inventory** | Warehouses, stock items, ledger, reservations | `IInventoryService`: `TryReserveAsync`, `ReleaseAsync`, `CommitAsync`, `ShipAsync`, `GetStockStatusAsync` |
| **Ordering** | Carts, orders, order lines, order status machine | `IOrderingService.MarkPaidAsync`, `GetOrderForPaymentAsync` |
| **Payment** | Payments, webhook handling, mock gateway | — |

### 5.3 Dependency rules

```
Ordering ──► Catalog     (prices, SKU info)
Ordering ──► Inventory   (reserve / release / ship)
Payment  ──► Ordering    (mark order paid)
```

- No cycles. **Inventory never depends on Ordering**; it only stores a reference (`ReferenceType`, `ReferenceId`).
- Modules call each other only through the public service interfaces above (`Rule.md` §2).
- Because all modules share one `AppDbContext`, a calling service may open a transaction and call another
  module's service inside it (e.g. Ordering creates the order and Inventory reserves stock in **one** transaction).
  The service that opens the transaction owns it.
- **No EF navigation properties across modules.** Entities reference other modules' data by id only
  (e.g. `OrderLine.SkuId`, not `OrderLine.Sku`). This keeps module boundaries real.
- The **mock payment gateway simulates an external system**: it talks to the Payment module only over HTTP
  (the webhook), never by calling services or the database directly.

---

## 6. Data model

Conventions for all tables:
- Primary keys: `Guid` generated with `Guid.CreateVersion7()` (sortable, not guessable).
- Times: `timestamptz`, always UTC.
- Money: `numeric(18,2)` ↔ C# `decimal`. Currency is VND only.
- Every table has `CreatedAt`; mutable tables also have `UpdatedAt`.
- Table and column names: snake_case in the database (configured via EF naming convention), PascalCase in C#.

### 6.1 Catalog

**categories**
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| Name | text | e.g. "Điện thoại" |
| Slug | text | unique, e.g. `dien-thoai` |
| ParentId | uuid? | FK → categories, max 2 levels |
| SortOrder | int | |

**products**
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| CategoryId | uuid | FK |
| Name | text | e.g. "Điện thoại Nova X 5G" |
| NameNormalized | text | lowercase, no diacritics, `đ`→`d`; set by C# on save; GIN trigram index |
| Slug | text | unique |
| Brand | text | fictional brand |
| Description | text | |
| Specs | jsonb | category-specific specs, e.g. `{"cpu": "...", "ramGb": 16}` |
| MinPrice | numeric(18,2) | lowest price among active SKUs; updated in the same transaction as any SKU change; used for price filter/sort |
| IsActive | bool | inactive products are hidden from the storefront |

**skus** (sellable variants)
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| ProductId | uuid | FK |
| SkuCode | text | unique, e.g. `NOVAX-128-BLK` |
| VariantName | text | e.g. "128GB - Đen" |
| Price | numeric(18,2) | current price, > 0 |
| Attributes | jsonb | e.g. `{"storage": "128GB", "color": "Đen"}` |
| IsActive | bool | |

### 6.2 Inventory

**warehouses**
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| Code | text | unique, e.g. `HN01`, `HCM01`, `DN01` |
| Name | text | |
| City | text | used for warehouse selection |
| IsActive | bool | |

**stock_items** — one row per (SKU, warehouse)
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| SkuId | uuid | FK |
| WarehouseId | uuid | FK |
| OnHand | int | physically in the warehouse |
| Reserved | int | held for unshipped orders |

Constraints: `UNIQUE (SkuId, WarehouseId)`, `CHECK (OnHand >= 0)`, `CHECK (Reserved >= 0)`,
`CHECK (Reserved <= OnHand)`.
`Available = OnHand - Reserved` is **computed, never stored**.
The CHECK constraints are a last line of defense; the code must never rely on them for normal flow.

**stock_movements** — the ledger (append-only: no UPDATE, no DELETE)
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| SkuId, WarehouseId | uuid | |
| Type | text | `Receipt`, `Shipment`, `Adjustment` |
| QuantityDelta | int | change to `OnHand` (+ or −, never 0) |
| OnHandAfter | int | `OnHand` right after this movement |
| ReferenceType | text? | e.g. `Order`, `GoodsReceipt` |
| ReferenceId | uuid? | |
| Reason | text? | required for `Adjustment` |
| CreatedBy | uuid | user id |
| CreatedAt | timestamptz | |

Index: `(SkuId, WarehouseId, CreatedAt, Id)`.
**Invariant:** for every stock item, `SUM(QuantityDelta) == OnHand`.
Reservations do **not** create ledger rows (they change `Reserved`, not `OnHand`).

**stock_reservations**
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderId | uuid | reference only (no FK to Ordering tables) |
| SkuId, WarehouseId | uuid | |
| Quantity | int | > 0 |
| Status | text | `Active`, `Committed`, `Released`, `Shipped` |

The payment deadline lives on the order (`orders.ExpiresAt`), not on the reservation.

### 6.3 Ordering

**carts**: `Id`, `CustomerId` (unique), `UpdatedAt`
**cart_items**: `CartId`, `SkuId`, `Quantity` (1–5); PK `(CartId, SkuId)`

**orders**
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderNumber | text | unique, human-friendly, e.g. `GN-20261121-000123` |
| CustomerId | uuid | owner |
| Status | text | see §7.3 |
| WarehouseId | uuid | fulfilling warehouse |
| Subtotal, Total | numeric(18,2) | Total = Subtotal (no shipping fee/discount in scope) |
| IdempotencyKey | text | `UNIQUE (CustomerId, IdempotencyKey)` |
| RequestHash | text | SHA-256 of the normalized checkout request (§7.7) |
| ShippingName, ShippingPhone, ShippingAddress, ShippingCity | text | |
| ExpiresAt | timestamptz | payment deadline |
| PaidAt, ShippedAt, CompletedAt, CancelledAt | timestamptz? | |
| CancelReason | text? | `Customer`, `Expired` |

**order_lines**
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderId | uuid | FK |
| SkuId | uuid | |
| ProductNameSnapshot, VariantNameSnapshot, SkuCodeSnapshot | text | copied at checkout |
| UnitPrice | numeric(18,2) | copied at checkout |
| Quantity | int | |
| LineTotal | numeric(18,2) | `UnitPrice * Quantity` |

### 6.4 Payment

**payments**
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| OrderId | uuid | |
| Provider | text | `Mock` |
| ProviderTransactionId | text? | **unique** — used to deduplicate webhooks |
| Amount | numeric(18,2) | |
| Status | text | `Pending`, `Succeeded`, `Failed`, `RefundRequired`, `Refunded` |
| CreatedAt, UpdatedAt | timestamptz | |

### 6.5 Identity

ASP.NET Core Identity tables (users, roles, user roles). Extra user fields: `FullName`, `Phone`, `City`.
Plus `data_protection_keys` — ASP.NET Core Data Protection keys (they encrypt auth cookies) stored in the
database so that redeploying the container does not log every user out (§11.5).

### 6.6 Indexes, sequences and generated values

| Table | Index / constraint | Used by |
|---|---|---|
| products | unique `Slug`; `(CategoryId)`; `(MinPrice)`; GIN trigram on `NameNormalized` | catalog list, search |
| skus | unique `SkuCode`; `(ProductId)` | product detail, checkout |
| stock_items | unique `(SkuId, WarehouseId)`; `(WarehouseId)` | reservation, stock overview |
| stock_movements | `(SkuId, WarehouseId, CreatedAt, Id)` | ledger page (keyset) |
| stock_reservations | `(OrderId)` | release / commit / ship |
| orders | unique `OrderNumber`; unique `(CustomerId, IdempotencyKey)`; `(CustomerId, CreatedAt DESC)`; `(Status, CreatedAt)`; partial `(ExpiresAt) WHERE Status = 'PendingPayment'` | my orders, admin list, **expiry job** |
| order_lines | `(OrderId)` | order detail |
| payments | unique `ProviderTransactionId`; `(OrderId)` | webhook dedup |

- **Order number:** PostgreSQL sequence `order_number_seq` (safe under concurrency).
  Format `GN-{yyyyMMdd}-{seq:D6}`, date in Vietnam time (`Asia/Ho_Chi_Minh`). The sequence never resets; gaps are allowed.
- **Time source:** the application's `TimeProvider` is the only clock (never the database's `now()`),
  so tests can control time.

---

## 7. Business rules

### 7.1 Stock quantities

- `OnHand`: units physically in the warehouse.
- `Reserved`: units promised to orders that are not yet shipped.
- `Available = OnHand - Reserved`: units that can still be sold.
- Customers never see exact numbers. Stock status shown to customers:
  - `InStock`: total available across active warehouses > 5
  - `LowStock`: 1–5
  - `OutOfStock`: 0
- Staff and Admin see exact numbers per warehouse.
- Stock status sums all warehouses, but an order is fulfilled from **one** warehouse (§7.4).
  So checkout can still fail with `409` even when the page said "In stock"; the UI must handle it.

### 7.2 Reserving stock (the most important rule)

Reserving `q` units of a SKU in a warehouse is **one atomic conditional SQL update**:

```csharp
var affected = await db.StockItems
    .Where(s => s.SkuId == skuId
             && s.WarehouseId == warehouseId
             && s.OnHand - s.Reserved >= quantity)
    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Reserved, x => x.Reserved + quantity), ct);
// affected == 1 → reserved; affected == 0 → not enough stock
```

- Never read stock, check it in C#, then save.
- An order with several lines reserves all lines **inside one transaction**; if any line fails, roll back everything.
- Reserve lines **sorted by `SkuId`** to keep a consistent lock order and avoid deadlocks.

### 7.3 Order lifecycle

```
                ┌──────────► Cancelled (customer cancels)
                │
PendingPayment ─┼──────────► Expired   (payment deadline passed)
                │
                └─► Paid ──► Shipped ──► Completed
```

| Transition | Who | Stock effect |
|---|---|---|
| (new) → `PendingPayment` | Customer checkout | `Reserved += q`; reservation `Active` |
| `PendingPayment` → `Paid` | Payment webhook | reservation `Committed` (stays reserved, no expiry) |
| `PendingPayment` → `Cancelled` | Customer | `Reserved -= q`; reservation `Released` |
| `PendingPayment` → `Expired` | Expiry job | `Reserved -= q`; reservation `Released` |
| `Paid` → `Shipped` | WarehouseStaff | `OnHand -= q`, `Reserved -= q`; ledger `Shipment`; reservation `Shipped` |
| `Shipped` → `Completed` | WarehouseStaff (simulated delivery) | none |

- Any other transition is rejected (`409 Conflict`).
- Status changes use a conditional update on the current status
  (`... WHERE Id = @id AND Status = 'PendingPayment'`), so two competing actions
  (e.g. payment vs expiry) can never both win.
- Cancelling a `Paid` order is out of scope.

### 7.4 Warehouse selection (no order splitting)

1. Build the candidate list: active warehouses, ordered by `City == ShippingCity` first, then by `Code`.
2. For each candidate, try to reserve **all** lines in one transaction.
3. The first warehouse that succeeds fulfils the whole order.
4. If none can fulfil the whole order → `409 Conflict` with the SKUs that are short.

### 7.5 Pricing

- The server reads current SKU prices at checkout. Any price sent by the client is ignored.
- Prices, names and SKU codes are copied into `order_lines` (snapshot). Later price changes do not affect existing orders.
- `LineTotal = UnitPrice * Quantity`, `Total = Σ LineTotal`. VND has no decimals in practice; still use `decimal`.
- Inactive products/SKUs cannot be ordered.

### 7.6 Cart

- One cart per customer. Quantity per line: 1–5.
- Adding to cart does **not** reserve stock.
- Cart shows current prices and stock status; checkout re-validates everything.
- After a successful checkout, the ordered items are removed from the cart **in the same transaction**.
- **Limit: at most 3 `PendingPayment` orders per customer.** A 4th checkout returns `409 too_many_pending_orders`.
  Without this, one user could lock all stock by placing many unpaid orders.

### 7.7 Idempotency of checkout

- `POST /api/orders` requires header `Idempotency-Key` (client-generated UUID, one per checkout attempt).
- Unique constraint `(CustomerId, IdempotencyKey)`.
- `RequestHash` = SHA-256 of the normalized request (cart lines sorted by `SkuId` + shipping info).
- Same key + same hash → return the **existing** order with the same response (`200`), do not create a new one.
- Same key + **different** hash → `409 idempotency_key_reused`.
- Failed attempts (e.g. out of stock) roll back and store nothing, so retrying with the same key tries again.
- Two concurrent requests with the same key → one inserts, the other gets a unique violation.
  In PostgreSQL that error **aborts the transaction**: roll it back, then read the existing order with a new query.

### 7.8 Reservation expiry

- Payment deadline: `ExpiresAt = CreatedAt + 15 minutes` (configurable: `Ordering:PaymentTimeoutMinutes`).
- A `BackgroundService` runs every 60 seconds and processes expired `PendingPayment` orders in batches of 100.
- For each order: conditional status update to `Expired`; only if that succeeds, release its reservations.
- The job must be safe to run on multiple instances in the future: per-order conditional updates make it so.
- The job stops cleanly on shutdown (`CancellationToken`); each order is its own transaction, so a half-finished batch is safe.
- Order responses include `ExpiresAt` **and** `ServerNow`; the frontend countdown uses the difference,
  so a wrong clock on the customer's device does not matter.

### 7.9 Payment (mock)

- Customer starts payment → a `Pending` payment is created → customer is sent to a mock payment page.
- The mock gateway sends a webhook to `POST /api/payments/webhook`:
  - Body: `providerTransactionId`, `orderId`, `amount`, `status` (`Succeeded` / `Failed`).
  - Header `X-Timestamp`: Unix seconds when the webhook was sent.
  - Header `X-Signature`: HMAC-SHA256 of `"{timestamp}.{rawBody}"` with a shared secret (from configuration/secrets).
- The mock gateway sends webhooks from a **background worker** (in-memory `Channel`), never from the request thread,
  and retries a non-2xx response up to 3 times (after 1 s, 5 s, 25 s) — like real gateways, which is exactly why
  the webhook must be idempotent.
- The storefront learns the result by **polling** `GET /api/orders/{id}` every 2 s for up to 60 s
  (no WebSocket/SignalR in scope).
- Webhook handling:
  1. Verify signature with a constant-time comparison, and reject timestamps older than 5 minutes
     (replay protection) → `401`, log warning.
  2. Deduplicate by `ProviderTransactionId` (unique) → already processed: return `200`, do nothing.
  3. Amount must equal order total → otherwise mark payment `Failed`, log warning.
  4. `Succeeded` and order still `PendingPayment` → order `Paid`, reservation `Committed`.
  5. `Succeeded` but order already `Expired`/`Cancelled` → payment `RefundRequired`, then mock refund → `Refunded`.
  6. `Failed` → payment `Failed`; order stays `PendingPayment` until it expires (customer may retry).
- The mock gateway endpoint (`/api/mock-gateway/...`) exists **only** in `Development` and `Demo` environments.
  It can simulate: success, failure, duplicate webhook (sent twice), delayed webhook.
  The caller must own the payment (a customer cannot "pay" someone else's order, even in Demo).

### 7.10 Inventory operations

- **Goods receipt** (WarehouseStaff, Admin): add quantity for one or more SKUs to a warehouse.
  `OnHand += q` and a `Receipt` ledger row per line, in one transaction.
- **Adjustment** (Admin only): positive or negative correction with a required reason (e.g. stock count, damage).
  A negative adjustment cannot make `OnHand < Reserved`.
- **Shipment** (WarehouseStaff): only for `Paid` orders; see §7.3.

### 7.11 Access control

- Customer endpoints always filter by the current user id from the auth cookie.
- Accessing another customer's order returns `404` (not `403`, to avoid revealing that it exists).
- Admin/Staff endpoints are under `/api/admin/...` and protected by role policies.

---

## 8. Key flows (step by step)

### 8.1 Checkout — `POST /api/orders`

**Before the transaction** (keep the transaction short — locked stock rows stay locked until commit):

1. Require authentication (Customer), the CSRF header (§11.5) and `Idempotency-Key`.
2. Validate input (shipping info, at least one cart line). Compute `RequestHash`.
3. If an order with this key exists for this customer → same hash: return it; different hash: `409 idempotency_key_reused`.
4. If the customer already has 3 `PendingPayment` orders → `409 too_many_pending_orders`.
5. Load cart lines; load SKUs from Catalog (active only, current prices). Compute line totals and total.
6. Build the candidate warehouse list (§7.4).

**For each candidate warehouse, one transaction:**

7. Reserve every line, sorted by `SkuId` (§7.2). Any line fails → roll back, try the next warehouse.
8. Get the next order number; insert order (`PendingPayment`, `ExpiresAt`), order lines (snapshots), reservations (`Active`).
9. Remove the ordered items from the cart.
10. Commit. On deadlock/serialization failure, retry the whole transaction (§11.3).

**After:**

11. Return `201` with order id, number, total, `ExpiresAt`, `ServerNow`.
12. Errors: no warehouse can fulfil → `409 out_of_stock` with short SKUs; invalid input → `400`; not logged in → `401`.

### 8.2 Payment webhook — `POST /api/payments/webhook`

Anonymous endpoint, protected by signature. Steps in §7.9. Always fast; no external calls inside the transaction.

### 8.3 Expiry job

Every 60 s: select up to 100 order ids where `Status = PendingPayment AND ExpiresAt < now` →
for each: transaction { conditional status update → if 1 row: release reservations (`Reserved -= q`, status `Released`) }.
Log the number of expired orders at `Information`.

### 8.4 Goods receipt — `POST /api/admin/stock/receipts`

Validate lines → transaction { for each line: upsert stock item, `OnHand += q`, insert `Receipt` movement with `OnHandAfter` } → `201`.

### 8.5 Shipment — `POST /api/admin/orders/{id}/ship`

Transaction { conditional status update `Paid → Shipped` → for each reservation: `OnHand -= q`, `Reserved -= q`,
insert `Shipment` movement, reservation `Shipped` } → `200`.

### 8.6 Customer cancel — `POST /api/orders/{id}/cancel`

Owner only. Transaction { conditional update `PendingPayment → Cancelled` → release reservations } → `200`.
Any other status → `409`.

---

## 9. API endpoints

All routes are prefixed with `/api`. All list endpoints are paginated (`Rule.md` §3).

| Method | Route | Role | Notes |
|---|---|---|---|
| POST | `/auth/register` | Anonymous | rate limited |
| POST | `/auth/login` | Anonymous | rate limited |
| POST | `/auth/logout` | Any signed-in | |
| GET | `/auth/me` | Any signed-in | id, name, role |
| GET | `/categories` | Anonymous | cached |
| GET | `/products` | Anonymous | `q`, `categoryId`, `brand`, `minPrice`, `maxPrice`, `sort`, `page`, `pageSize`; cached |
| GET | `/products/{slug}` | Anonymous | product + SKUs; cached 60 s |
| GET | `/products/{slug}/stock-status` | Anonymous | stock status per SKU; cached ≤ 10 s |
| GET | `/cart` | Customer | |
| PUT | `/cart/items/{skuId}` | Customer | body `{ quantity }` |
| DELETE | `/cart/items/{skuId}` | Customer | |
| POST | `/orders` | Customer | header `Idempotency-Key`; rate limited |
| GET | `/orders` | Customer | own orders only |
| GET | `/orders/{id}` | Customer | own order only, else `404` |
| POST | `/orders/{id}/cancel` | Customer | own, `PendingPayment` only |
| POST | `/orders/{id}/payments` | Customer | creates mock payment, returns payment page URL |
| POST | `/payments/webhook` | Anonymous + signature | |
| POST | `/mock-gateway/payments/{paymentId}/simulate` | Dev/Demo only | `{ outcome, duplicate, delaySeconds }` |
| GET/POST/PUT | `/admin/categories[...]` | Admin | |
| GET/POST/PUT | `/admin/products[...]`, `/admin/products/{id}/skus[...]` | Admin | invalidates catalog cache |
| GET/POST/PUT | `/admin/warehouses[...]` | Admin (GET: Staff too) | |
| GET | `/admin/stock` | Staff, Admin | filter by warehouse, SKU, low stock |
| POST | `/admin/stock/receipts` | Staff, Admin | |
| POST | `/admin/stock/adjustments` | Admin | reason required |
| GET | `/admin/stock/movements` | Staff, Admin | keyset pagination (`cursor`) |
| GET | `/admin/orders` | Staff, Admin | filter by status |
| POST | `/admin/orders/{id}/ship` | Staff | `Paid` only |
| POST | `/admin/orders/{id}/complete` | Staff | `Shipped` only |
| GET | `/health/live` | Anonymous | process is running (used by Docker healthcheck) |
| GET | `/health/ready` | Anonymous | database reachable (used by uptime monitor and deploy check) |
| POST | `/assistant/chat` | Customer | **bonus B1**, rate limited, feature flag |

Status codes: `200`/`201` success, `400` validation, `401` not signed in, `403` wrong role,
`404` not found or not owned, `409` business conflict (out of stock, invalid status), `429` rate limited.
All errors use ProblemDetails with a stable `code` and the request's `traceId`.
Business error codes: `out_of_stock`, `invalid_status`, `too_many_pending_orders`, `idempotency_key_reused`.

Response shapes:
- Page-based lists: `{ items, page, pageSize, totalCount }` (default `pageSize` 20, max 100).
- Keyset lists: `{ items, nextCursor }` (`nextCursor` is `null` on the last page).
- API versioning: none (single client we control). Breaking changes update the generated frontend types in the same PR.

---

## 10. Search design (P6)

- `NameNormalized` is computed in C# on every product save: lowercase → remove diacritics
  (Unicode normalization FormD + drop non-spacing marks) → replace `đ` with `d` → collapse spaces.
- The same function normalizes the user's query.
- Query: `NameNormalized ILIKE '%term%'` (or `%` similarity) backed by a **GIN trigram index** (`pg_trgm`).
- Filters: category (including sub-categories), brand, price range (min SKU price). Sort: relevance, price asc/desc, newest.
- The normalization function has unit tests with Vietnamese cases (`Điện thoại` → `dien thoai`, `Đồng hồ` → `dong ho`).

---

## 11. Non-functional design

### 11.1 Targets and capacity

These are **targets to verify**, not facts. After the first k6 run, adjust them and record the change in §19.

| Area | Target |
|---|---|
| Deployment | 1 VPS (2 vCPU / 4 GB RAM), 1 API instance, 1 PostgreSQL |
| Catalog reads | p95 < 200 ms at 200 requests/s (with cache) |
| Checkout | p95 < 500 ms at 50 requests/s, normal stock |
| Flash buy (L2) | 1,000 concurrent buyers, 1 unit → 1 success, no errors other than `409`, all responses within 10 s |
| Correctness | 0 oversold units and ledger invariant holds, in every test |
| Availability | Single server, no high availability (accepted for a demo); containers restart automatically |
| Data loss tolerance (RPO) | up to 24 h (daily backup) |
| Recovery time (RTO) | < 1 h (restore latest backup) |

Back-of-envelope:
- 100,000 orders × ~2 lines ≈ 300,000 rows, roughly 100 MB with indexes. The ledger grows at a similar rate.
  → One PostgreSQL instance is plenty; no sharding or partitioning.
- Connections: API pool `Maximum Pool Size=50`, PostgreSQL `max_connections=100`
  (room left for migrations, backups, admin tools). Pool wait `Timeout=15` s.
- 1,000 concurrent requests therefore **queue for connections** — expected; the 10 s target accounts for it.

### 11.2 Performance (P7)

- **Keep transactions short.** Validate, price and choose candidate warehouses **before** opening the checkout
  transaction (§8.1). A reserved stock row stays locked until commit, so every extra millisecond inside the
  transaction slows every buyer of the same SKU.
- **Catalog caching** with HybridCache: categories, product list pages, product detail. Expiry 60 s.
  Product/SKU/price changes remove the affected keys. Stock status has its own endpoint and a short cache (≤ 10 s).
  The product list does not show stock status (keeps list caching simple).
- **Never** use cached stock for reservation decisions.
- **Rate limiting** policies:
  - `auth`: 5 requests/minute per IP (login, register)
  - `checkout`: 10 requests/minute per user
  - `assistant`: 20 requests/hour per user (bonus)
  - global: 100 requests/10 seconds per IP
  - Rejected requests return `429` with `Retry-After`.
- **Queries:** `AsNoTracking`, projection with `Select`, pagination, indexes on filtered columns.
- **Measurement with k6** (scripts in `tests/load/`):
  - `L1-catalog-spike.js`: ramp to N virtual users browsing products; run **before** and **after** caching.
  - `L2-flash-buy.js`: 1,000 virtual users try to buy the same 1-unit SKU.
  - Record p95 latency, error rate and machine specs in `README.md`. Only real measured numbers.
  - Run k6 in the `LoadTest` environment (§11.6): rate limiting off (otherwise every k6 request shares one IP
    and gets `429`), 1,000 seeded load-test customers, logged in during k6 `setup()`.
  - Run k6 from a **different machine** than the server; otherwise k6 uses the server's CPU and the numbers are wrong.
  - Test the rate limiter separately (integration test + a small k6 script expecting `429`).

### 11.3 Reliability and failure modes

| Failure | Required behavior |
|---|---|
| API crashes in the middle of checkout | Transaction never committed → rolled back by PostgreSQL; nothing reserved, no order |
| Double click / client retry | Idempotency key (§7.7) |
| Webhook duplicated or retried | Deduplicated by `ProviderTransactionId` (§7.9) |
| Webhook never arrives | Order expires, stock released; a later webhook → `RefundRequired` |
| Expiry job crashes / server restarts | All state is in the database; the next run continues |
| Shutdown during a job | Honors `CancellationToken`; one transaction per order |
| Database unavailable | `/health/ready` unhealthy; API returns `503` ProblemDetails; the app still starts (no DB work at startup) |
| Deadlock or serialization failure (`40P01`, `40001`) | Retry the whole transaction up to 3 times with a short random delay (checkout, shipment, expiry) |
| Disk full | Prevented by log rotation and backup retention (§11.6) |
| Bad deployment | Redeploy the previous image tag (§11.6) |

Database rules:
- Use PostgreSQL's default isolation level (`READ COMMITTED`); the atomic conditional update is correct under it.
- Do **not** enable EF Core `EnableRetryOnFailure` globally — it conflicts with manually opened transactions.
  Retry explicitly in the places listed above.
- After any database error inside a transaction (e.g. unique violation), the transaction is aborted:
  roll back, then run follow-up queries outside it.

### 11.4 Observability

- Structured logs to stdout (`Rule.md` §6); Docker collects them.
- Every request has a trace id, written in every log line and returned as `traceId` in every ProblemDetails,
  so an error a user reports can be found in the logs.
- Log business events: order created / expired / paid / shipped, stock received / adjusted, webhook rejected.
- `/health/live` for Docker's healthcheck (restart only if the process is stuck, not when the DB is down);
  `/health/ready` for monitoring and the deploy check.
- An external uptime monitor (any free service) checks `/health/ready` every 5 minutes and alerts the human.
- OpenTelemetry traces/metrics: optional, only if time allows (W5-02).

### 11.5 Security design

- **Auth cookie:** HttpOnly, Secure, `SameSite=Strict`, 7-day sliding expiration.
- **API never redirects to a login page:** cookie events return `401` / `403` (ASP.NET Core redirects by default).
- **CSRF:** besides `SameSite=Strict`, every state-changing request (POST/PUT/PATCH/DELETE) must send header
  `X-CSRF: 1`; requests without it get `403`. Browsers cannot add custom headers to cross-site requests
  without a CORS preflight, which we do not allow. The webhook endpoint is exempt (it uses its signature).
- **Data Protection keys** stored in PostgreSQL (`data_protection_keys`), so redeploys do not invalidate cookies.
- **Identity:** password ≥ 8 characters; lockout for 15 minutes after 5 failed logins;
  login errors say "email or password is incorrect" without saying which.
- **Reverse proxy:** enable Forwarded Headers middleware, trusting only the Caddy container,
  so the real client IP (for rate limiting and logs) and HTTPS scheme are used.
  Without it, every user shares Caddy's IP and one rate limit.
- **Security headers** (set in Caddy): HSTS, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: strict-origin-when-cross-origin`, a basic Content-Security-Policy.
- **Webhook:** signature + timestamp + constant-time comparison (§7.9).
- **Secrets:** user-secrets in development; environment variables from a `.env` file (permission 600, not in git) on the VPS.
- **Dependencies:** CI runs `dotnet list package --vulnerable` and `npm audit --audit-level=high`; Dependabot enabled.
- **Public demo:** rate limits on; spending limit set at the AI provider (bonus B1).

### 11.6 Environments, deployment and operations

| Environment | Purpose | Differences |
|---|---|---|
| `Development` | local work | mock gateway on, demo seed, user-secrets |
| `Test` | integration tests | Testcontainers, test authentication scheme (§13.2) |
| `LoadTest` | k6 runs | rate limiting off, 1,000 load-test customers seeded |
| `Demo` | public deployment | mock gateway on, demo seed, payment timeout 1 minute |

- Configuration uses options classes validated at startup (`ValidateOnStart`), so missing config fails immediately.
- **CI** (every push/PR): build, tests, lint, vulnerability checks.
- **Images:** on merge to `main`, build Docker images tagged with the git commit SHA and push to GitHub Container Registry.
- **Deploy** (manual `workflow_dispatch`, over SSH): back up the DB → run the EF Core **migration bundle** →
  `docker compose pull && docker compose up -d` → wait for `/health/ready`.
- **Migrations** never run automatically at app startup. They must be backward compatible with the previous
  app version (add new columns as nullable first; drop old columns only in a later release), so rollback works.
- **Rollback:** redeploy the previous image tag. Restoring a backup is the last resort.
- **Backups:** daily `pg_dump`, keep 7 days, copy off the VPS. **Restore test done once before the demo.**
- PostgreSQL data in a named Docker volume. All containers `restart: unless-stopped`.
- Docker log rotation: `max-size: 10m`, `max-file: 3`.

### 11.7 Scaling constraints (known and accepted)

The project assumes **one API instance**. What would break with 2+ instances, and the fix:

| Component | Problem with several instances | Fix (explain, do not build) |
|---|---|---|
| In-memory cache | invalidation on one instance does not reach others | Redis as shared cache, or very short expiry |
| Rate limiter | each instance counts separately | Redis-backed limiter or limit at the proxy/gateway |
| Expiry job | runs on every instance | already safe (conditional updates); add `FOR UPDATE SKIP LOCKED` to avoid duplicate work |
| Data Protection keys | — | already shared via the database |
| Mock gateway queue | in memory, lost on restart | acceptable for a mock |

---

## 12. Frontend

### 12.1 Storefront (`src/web/storefront`)

| Page | Notes |
|---|---|
| Home / product list | category menu, search box, filters, pagination (no stock status) |
| Product detail | SKU selector, price, stock status (separate request), add to cart |
| Cart | quantities, current prices, stock warnings |
| Checkout | shipping form (Zod), place order with idempotency key, disable button while pending; handle every `409` code |
| Mock payment page | buttons: Success / Fail / Duplicate webhook / Delayed; then poll order status |
| My orders, Order detail | status, countdown from `ExpiresAt` − `ServerNow`, cancel button |

All state-changing requests send the `X-CSRF: 1` header (set once in the shared API client).
| Login / Register | |
| Chat widget | **bonus B1** |

### 12.2 Admin (`src/web/admin`)

| Page | Role |
|---|---|
| Products & SKUs | Admin |
| Categories | Admin |
| Warehouses | Admin |
| Stock overview (per warehouse, low-stock filter) | Staff, Admin |
| Goods receipt form | Staff, Admin |
| Stock adjustment form | Admin |
| Stock ledger (per SKU/warehouse) | Staff, Admin |
| Orders list + Ship / Complete actions | Staff, Admin |

UI rules: handle loading, empty and error states; show clear messages for `409` (out of stock / invalid status)
and `429`. Keep design simple (Tailwind); do not spend time on animations.

---

## 13. Testing strategy

### 13.1 Unit tests
- Vietnamese text normalization.
- Price/total calculation.
- Order status transition rules.
- Validators.
- Webhook signature verification.

### 13.2 Integration tests (Testcontainers + PostgreSQL)

Most tests use a **test authentication scheme** (a header selects the user and role) so that parallel tests do not
pay for password hashing. A few tests use the real cookie login to cover it.

- Auth: `401` when signed out (not a `302` redirect), `403` with wrong role for admin endpoints.
- Missing `X-CSRF` header on a POST → `403`.
- Login lockout after 5 failed attempts.
- **IDOR:** customer B requesting customer A's order → `404`.
- Checkout success; out of stock → `409`; client-sent price is ignored.
- Same `Idempotency-Key` twice → one order, same response; same key with a different cart → `409 idempotency_key_reused`.
- 4th unpaid order → `409 too_many_pending_orders`.
- Goods receipt and adjustment write ledger rows; negative adjustment below `Reserved` → `409`.
- Webhook: bad signature → `401`; timestamp older than 5 minutes → `401`; amount mismatch → payment `Failed`.
- Checkout response contains `ServerNow`; error responses contain `traceId`.
- Shipment updates `OnHand`, `Reserved` and ledger.
- Search: `dien thoai` finds `Điện thoại`.

### 13.3 Concurrency tests (required, see `Rule.md` §9)

| ID | Scenario | Expected |
|---|---|---|
| **C1** | 200 parallel checkouts (different customers) for a SKU with 1 unit | exactly 1 success, 199 × `409`, `Reserved == 1`, `OnHand - Reserved >= 0` |
| **C2** | 50 parallel checkouts with the same `Idempotency-Key` | exactly 1 order |
| **C3** | Same successful webhook sent 10 times in parallel | 1 payment applied, order `Paid` once |
| **C4** | Expiry job and successful webhook race on the same order | exactly one of `Paid` / `Expired`; stock consistent; late payment → `RefundRequired` |
| **C5** | Random mix of receipts, adjustments, checkouts, cancels, shipments | for every stock item: `SUM(ledger deltas) == OnHand`, `0 <= Reserved <= OnHand` |

C1 uses 200 requests to stay CI-friendly; the 1,000-user version runs with k6 (`L2-flash-buy.js`).

### 13.4 Seed data (Bogus, deterministic seed)
- 4 top-level categories: Phones, Laptops, PC components, Accessories (+ a few sub-categories).
- ~40 products, ~120 SKUs, fictional brands, Vietnamese product names with diacritics.
- 3 warehouses: `HN01` (Hà Nội), `HCM01` (TP.HCM), `DN01` (Đà Nẵng).
- **Initial stock is created through the goods-receipt service**, never by inserting `stock_items` directly,
  so the ledger invariant (`SUM(deltas) == OnHand`) holds from the start.
- Seeding is idempotent (running it twice changes nothing).
- `LoadTest` only: 1,000 customers `load-0001@test.local` … `load-1000@test.local`.
- Demo accounts (Development/Demo only): `customer1@demo.local`, `customer2@demo.local`,
  `staff@demo.local`, `admin@demo.local`. Demo passwords are documented in README and set via configuration.
- One SKU with exactly 1 unit for the flash-buy demo.

---

## 14. Roadmap and task list

Legend: ⭐ = core task. The human should try writing it first, or at least fully understand it (see `CLAUDE.md` §7).
Each task follows the full flow in `Rule.md` §1.

**Tracking with GitHub Issues:**
- This list is the **plan**. Each task gets a **GitHub issue** (its spec) when work on it starts, created by `/to-spec`.
  Issues are not created in advance, so the plan and the issues never drift apart.
- Add the issue link after the task ID when it is created, e.g. `**W3-03** (#14) Checkout ...`.
- Each issue belongs to the GitHub milestone of its week (`Week 1` … `Week 5`, `Bonus`).
- A task is done when its PR (`Closes #14`) is merged. Then mark `[x]` here and in `Workflow.vi.md`.

**Current phase:** Week 1 — not started

### Week 1 — Foundation & Catalog

- [ ] **W1-01** Repository setup: solution, folders per `Rule.md` §2, `.editorconfig`, `Directory.Build.props`
  (nullable, warnings as errors), `docker-compose.yml` (PostgreSQL), `.gitignore`, README skeleton.
  *Done when:* `docker compose up -d` + `dotnet run` starts the API.
- [ ] **W1-02** CI with GitHub Actions: backend build + tests, frontend lint + build, vulnerability checks; Dependabot.
  *Done when:* a PR shows green checks.
- [ ] **W1-03** Data foundation: `AppDbContext`, snake_case naming, UTC/decimal conventions, `TimeProvider`,
  connection pool settings, options validation at startup, first migration, `/health/live` and `/health/ready`.
- [ ] **W1-04** Error handling & API docs: ProblemDetails with `code` + `traceId`, FluentValidation wiring,
  standard list response types (§9), OpenAPI + Scalar.
- [ ] **W1-05** Identity: register, login, logout, me; cookie settings (§11.5); `401`/`403` instead of redirects;
  CSRF header check; lockout; Data Protection keys in DB; roles + seeded staff/admin; authorization policies;
  test authentication scheme for integration tests.
  *Done when:* tests prove `401`, `403`, missing-CSRF and lockout cases.
- [ ] **W1-06** Catalog admin: categories, products, SKUs (DTOs, validators, slug generation, `MinPrice` maintenance).
- [ ] **W1-07** Catalog public: product list (filters, pagination) and product detail.
- [ ] **W1-08** Seed data with Bogus (§13.4): categories, products, SKUs, users. **No stock yet** (added in W2-03).
- [ ] **W1-09** Frontend setup: both Vite apps, routing, Tailwind, TanStack Query, API type generation,
  login page, storefront product list/detail, admin product management.

### Week 2 — Inventory

- [ ] **W2-01** Warehouses (admin).
- [ ] **W2-02** Stock items table with CHECK constraints and unique index.
- [ ] ⭐ **W2-03** Goods receipt with ledger rows in the same transaction; extend the seed to create initial stock
  through this service.
- [ ] **W2-04** Stock adjustment (Admin, reason required, cannot go below `Reserved`).
- [ ] **W2-05** Stock overview and ledger query (keyset pagination).
- [ ] **W2-06** Stock status endpoint (`InStock` / `LowStock` / `OutOfStock`) and display on product detail.
- [ ] **W2-07** Admin UI: warehouses, stock overview, receipt form, adjustment form, ledger page.

### Week 3 — Cart & Checkout

- [ ] **W3-01** Cart API.
- [ ] ⭐ **W3-02** Inventory reservation service: `TryReserve`, `Release`, `Commit`, `Ship` (atomic updates, §7.2).
- [ ] ⭐ **W3-03** Checkout (§8.1): warehouse selection, price snapshot, order number sequence,
  order + lines + reservations + cart cleanup in one transaction, idempotency key + request hash,
  pending-order limit, retry on deadlock/serialization failure.
- [ ] **W3-04** My orders list/detail with ownership checks (IDOR tests).
- [ ] **W3-05** Customer cancel.
- [ ] ⭐ **W3-06** Concurrency tests C1 and C2.
- [ ] **W3-07** Storefront: cart, checkout, my orders, order detail pages.

### Week 4 — Payment, expiry, search, performance

- [ ] **W4-01** Start payment + mock gateway (Dev/Demo only, HTTP-only, background sender with retries,
  ownership check) + mock payment page with status polling.
- [ ] ⭐ **W4-02** Payment webhook: timestamped signature, deduplication, amount check, mark paid; test C3.
- [ ] ⭐ **W4-03** Reservation expiry background job; late payment → `RefundRequired` → `Refunded`; test C4.
- [ ] **W4-04** Shipment and completion (staff); test C5.
- [ ] **W4-05** Vietnamese search: normalization, trigram index, filters.
- [ ] **W4-06** Catalog caching + rate limiting policies.
- [ ] **W4-07** `LoadTest` environment + 1,000 load-test accounts; k6 scripts L1 and L2 run from a separate machine;
  measure before/after; compare with §11.1 targets; write results to README.

### Week 5 — Ship it

- [ ] **W5-01** UI polish: loading/empty/error states, messages for `409` and `429`.
- [ ] **W5-02** Observability review (§11.4): structured logs, masking, trace ids, business events,
  uptime monitor. OpenTelemetry only if time allows.
- [ ] **W5-03** Production Dockerfiles and compose, Caddy (HTTPS, security headers), forwarded headers,
  images on GitHub Container Registry, manual deploy workflow with migration bundle, daily backups
  **and one restore test**, log rotation, Demo seeding (§11.5, §11.6).
- [ ] **W5-04** README complete (§16), check all ADRs in §17 exist, screenshots or short demo GIF.
- [ ] **W5-05** Rehearse the demo script (§15); final review of the whole codebase against `Rule.md`.

### Week 6 — Bonus B1: AI assistant (optional, only if Weeks 1–5 are done)

- [ ] **B1-01** Read-only tools: search products, get stock status, get my order status.
- [ ] **B1-02** `/api/assistant/chat` with Microsoft.Extensions.AI, rate limit, feature flag.
- [ ] **B1-03** Storefront chat widget showing product cards (links built by backend).
- [ ] **B1-04** Evaluation set: 30 questions with expected answers, runnable as a test.

AI rules: tools are read-only; user id from auth context only; links, prices and stock status come from
backend data, never from the model; treat all text as untrusted (`Rule.md` §11).

**If time runs out:** stop after Week 4, then do W5-03 and W5-04 (deploy + README). That is still a complete project.

---

## 15. Demo script (about 3 minutes)

1. Show the admin stock page: SKU `NOVAX-128-BLK` has 1 unit in `HCM01`.
2. Run `k6 run tests/load/L2-flash-buy.js` (1,000 buyers) → 1 success, 999 out-of-stock; stock not negative.
3. Place an order and do not pay → after the timeout (set to 1 minute in Demo) the order is `Expired` and stock is available again.
4. On the mock payment page, click "Duplicate webhook" → order paid once, one payment row.
5. Staff ships the order → open the ledger: every number is explained by a movement.
6. Log in as another customer and open the first customer's order URL → `404`.
7. Search `dien thoai` → Vietnamese results.
8. (Bonus) Ask the assistant "Nova X 128GB màu đen còn hàng không?" → answer with product card and link.

---

## 16. README checklist

- [ ] One-paragraph description + the core promise
- [ ] Live demo link + demo accounts per role
- [ ] Architecture diagram and order/stock state diagram
- [ ] "Hard problems and how I solved them": P1–P7, 3–4 lines each
- [ ] Test results: concurrency tests + k6 numbers with machine specs
- [ ] How to run locally with one command
- [ ] Tech stack
- [ ] Known limitations and failure handling (§11.3, §11.7)
- [ ] What I would do next at larger scale (§18)

---

## 17. Documentation in the repository

| What | Where | Created by |
|---|---|---|
| Spec of each task | GitHub issue (template of the `to-spec` skill) | `/grill-with-docs` → `/to-spec` |
| Domain vocabulary | `GLOSSARY.md` | `/grill-with-docs` (domain-modeling) |
| Design decisions | `docs/adr/NNNN-title.md` (context, decision, alternatives, consequences) | `/grill-with-docs`, or written when the decision is made |
| Agent skill configuration | `docs/agents/*.md` | `/setup-matt-pocock-skills` (once) |

Every spec issue must include in "Further Notes": `Task: W?-??` and the relevant `Workflow.md` sections.

ADRs to write (when the related task is done at the latest):
1. `0001-atomic-conditional-update-for-stock.md` — vs pessimistic locking vs optimistic concurrency token
2. `0002-append-only-stock-ledger.md`
3. `0003-idempotency-key-for-checkout.md`
4. `0004-cookie-auth-instead-of-jwt-in-browser.md`
5. `0005-no-redis-or-message-queue-yet.md`
6. `0006-csrf-protection-for-cookie-auth.md`
7. `0007-single-instance-deployment-and-backups.md`
8. `0008-github-issues-and-agent-skills-workflow.md`

---

## 18. Interview talking points (how it would scale — explain, do not build)

| Question | Answer direction |
|---|---|
| 100× more traffic? | More API instances (stateless), move cache to Redis, read replicas for catalog, CDN for frontend |
| Real flash sale? | Redis counter as a gate in front of the DB, a queue to process orders sequentially, "sold out" flag cache |
| Payment gateway down? | Retry with backoff, circuit breaker, reconciliation job asking the gateway for missed payments |
| Reliable events (email, analytics)? | Outbox table written in the same transaction + message broker (RabbitMQ) |
| Order needs items from 2 warehouses? | Split into shipments per warehouse, each with its own status |
| Why a modular monolith? | Simple to build, deploy and test; modules can be extracted later if a real need appears |

---

## 19. Plan change log

| Date | Change | Approved by |
|---|---|---|
| 2026-11-21 | Initial plan | human |
| 2026-11-22 | Product renamed to Gearnix (repository `gearnix`); order number prefix `GN-` | human |
| 2026-11-21 | Adopt mattpocock/skills: specs as GitHub issues (created per task), `GLOSSARY.md`, `docs/adr/`, test-first in vertical slices, independent `code-review`, human pushes and merges | human |
| 2026-11-21 | System design review: targets & capacity, reliability, observability, security, deployment & operations (§11); indexes & order number sequence (§6.6); idempotency request hash, pending-order limit, webhook replay protection & retries, cart cleanup inside checkout transaction; seed stock via goods receipts; stock status as separate endpoint | human |
