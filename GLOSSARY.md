# Glossary

Domain vocabulary for Gearnix. Use these exact terms in code, tests, issues, commits and docs.
Updated during `/grill-with-docs` sessions. Business rules behind each term: `Workflow.md` §7.

## Catalog

| Term | Meaning | Avoid saying |
|---|---|---|
| **Product** | A sellable item as customers see it (e.g. "Nova X 5G phone"). Has a name, brand, specs | item |
| **SKU** | A sellable variant of a product (e.g. "Nova X, 128GB, Black"). Prices and stock belong to SKUs | variant (in code) |
| **Category** | A group of products, at most 2 levels deep | |
| **Stock status** | What customers see: `InStock` (> 5 available), `LowStock` (1–5), `OutOfStock` (0) | stock level |
| **Slug** | The unique, URL-friendly name of a category or product (e.g. `dien-thoai-nova-x-5g`). Derived from the name once and never changed, so links keep working | URL name, permalink |
| **Active / Inactive** | An inactive product or SKU is hidden from the storefront and cannot be ordered. Catalog items are deactivated, never deleted | deleted, archived |
| **Specs** | A product's technical details (e.g. CPU, RAM), shown on the product page | properties |
| **Attributes** | What distinguishes one SKU of a product from another (e.g. storage, color) | options |
| **Min price** | The lowest price among a product's active SKUs; empty when it has none. Used to filter and sort products by price | starting price |

## Inventory

| Term | Meaning | Avoid saying |
|---|---|---|
| **Warehouse** | A physical location holding stock (`HN01`, `HCM01`, `DN01`) | store, branch |
| **Stock item** | The stock of one SKU in one warehouse | inventory row |
| **OnHand** | Units physically in the warehouse | quantity, stock |
| **Reserved** | Units promised to orders that are not shipped yet | held, locked |
| **Available** | `OnHand - Reserved`. Computed, never stored | free stock |
| **Reservation** | Stock held for one order; statuses `Active`, `Committed`, `Released`, `Shipped` | hold, lock |
| **Reserve / Release / Commit / Ship** | The four reservation operations (see `Workflow.md` §7.3) | |
| **Stock movement** | One row of the stock ledger: a change to `OnHand` (`Receipt`, `Shipment`, `Adjustment`) | transaction (ambiguous with DB transactions) |
| **Stock ledger** | The append-only list of stock movements; its sum always equals `OnHand` | history, log |
| **Goods receipt** | Adding received units to a warehouse (creates `Receipt` movements) | import |
| **Adjustment** | An Admin correction of `OnHand` with a reason | |

## Ordering & payment

| Term | Meaning | Avoid saying |
|---|---|---|
| **Cart** | A customer's list of SKUs before checkout. Does not reserve stock | basket |
| **Checkout** | Turning the cart into an order and reserving stock | purchase |
| **Order** | Statuses: `PendingPayment`, `Paid`, `Shipped`, `Completed`, `Cancelled`, `Expired` | |
| **Order line** | One SKU in an order, with a price snapshot | order item |
| **Snapshot** | Price/name copied into an order line at checkout | |
| **Payment deadline** | `orders.ExpiresAt`; after it an unpaid order becomes `Expired` | timeout (in UI text) |
| **Idempotency key** | Client-generated key that makes repeating a checkout safe | request id |
| **Payment** | One payment attempt; statuses `Pending`, `Succeeded`, `Failed`, `RefundRequired`, `Refunded` | |
| **Webhook** | HTTP call from the (mock) payment gateway reporting a payment result | callback |
| **Mock gateway** | The simulated external payment provider (Dev/Demo only) | fake payment |

## People

| Term | Meaning |
|---|---|
| **Customer** | Registered shopper; sees only their own data |
| **Warehouse staff** | Receives goods, ships orders (`WarehouseStaff` role) |
| **Admin** | Manages catalog, warehouses, adjustments |

## Security

| Term | Meaning |
|---|---|
| **IDOR** | Accessing another user's data by changing an id; prevented by ownership checks returning `404` |
| **CSRF header** | `X-CSRF: 1`, required on every state-changing request |
