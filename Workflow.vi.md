# Workflow.vi.md — Kế hoạch dự án Gearnix (bản tiếng Việt)

> **File này là gì:** bản dịch tiếng Việt của `Workflow.md`, dành cho người đọc.
> AI dùng bản tiếng Anh `Workflow.md`. **Nếu hai bản khác nhau, bản tiếng Anh là chuẩn.**
> Khi sửa kế hoạch, hãy sửa `Workflow.md` trước rồi cập nhật bản này cho khớp.
> Cách viết code được quy định trong `Rule.md`.

---

## 1. Tóm tắt dự án

**Gearnix** là hệ thống thương mại điện tử bán đồ điện tử (điện thoại, laptop, linh kiện máy tính, phụ kiện),
với **trọng tâm là quản lý tồn kho nhiều kho**.
Website bán hàng được làm đơn giản có chủ đích: nhiệm vụ chính của nó là tạo ra đơn hàng thật để thử thách phần kho.

- **Mục đích:** dự án portfolio để ứng tuyển **thực tập sinh .NET backend**.
- **Thứ tự ưu tiên:** làm xong > làm đúng > deploy được > giải thích được > nhiều tính năng.
- **Lời cam kết cốt lõi:** *"Không bao giờ bán vượt tồn kho, và mọi thay đổi tồn kho đều truy vết được."*
- **Tên sản phẩm:** "Gearnix" (repository `gearnix`). Tên chỉ dùng cho dự án portfolio này. Mọi thương hiệu và sản phẩm trong dữ liệu mẫu đều là hư cấu.
  Không sao chép tên, logo, hình ảnh hay giao diện của cửa hàng thật.

---

## 2. Mục tiêu và những gì không làm

### 2.1 Bài toán cốt lõi (bắt buộc)

| ID | Bài toán | Thế nào là xong |
|---|---|---|
| **P1** | Nhiều người mua, ít hàng | 1.000 lượt đặt hàng đồng thời cho 1 sản phẩm → đúng 1 đơn thành công, tồn kho không âm |
| **P2** | Giữ hàng có thời hạn | Đơn chưa thanh toán tự động trả lại hàng đã giữ sau 15 phút |
| **P3** | Idempotency | Bấm "Đặt hàng" hai lần chỉ tạo 1 đơn; webhook thanh toán gửi trùng chỉ được áp dụng 1 lần |
| **P4** | Sổ biến động kho | Mọi thay đổi `OnHand` đều có một dòng trong sổ chỉ-được-thêm; tổng sổ luôn bằng `OnHand` |
| **P5** | Tính giá phía server và phân quyền | Giá tính ở server và lưu bản sao vào đơn; người dùng không bao giờ xem được đơn của người khác (IDOR) |
| **P6** | Tìm kiếm tiếng Việt | Gõ `dien thoai` vẫn ra `Điện thoại`; lọc theo danh mục, thương hiệu, khoảng giá |
| **P7** | Tải đột biến (mức cơ bản) | Cache danh mục sản phẩm + rate limiting, có số liệu trước/sau đo bằng k6 |

### 2.2 Điểm cộng (chỉ làm sau khi xong toàn bộ phần cốt lõi)

| ID | Tính năng |
|---|---|
| **B1** | Trợ lý AI: trả lời "X còn hàng không?" từ dữ liệu kho thật và gửi kèm link sản phẩm |

### 2.3 Không làm (trừ khi người dùng yêu cầu rõ ràng)

Redis, RabbitMQ, Outbox pattern, microservices, hàng đợi flash sale, voucher/mã giảm giá, combo/quà tặng,
quản lý serial/IMEI, tách một đơn ra nhiều kho, chuyển kho, đổi trả/bảo hành, bán đa kênh (sàn TMĐT, POS),
cổng thanh toán thật, đơn vị vận chuyển thật, đánh giá, wishlist, gợi ý sản phẩm, so sánh sản phẩm,
kiểm tra tương thích build PC, thu cũ đổi mới, trả góp, hóa đơn điện tử, gửi email, Kubernetes.

> Những mục này được loại khỏi phạm vi có chủ đích. Bạn chỉ cần *giải thích được* cách thêm chúng
> (xem mục 18), không cần làm.

---

## 3. Người dùng và vai trò

| Vai trò | Được làm |
|---|---|
| **Khách vãng lai** | Xem và tìm sản phẩm, xem trạng thái hàng (Còn hàng / Sắp hết / Hết hàng) |
| **Customer** | Như trên + giỏ hàng, đặt hàng, thanh toán (giả lập), xem và hủy đơn **của mình** |
| **WarehouseStaff** | Xem tồn kho và sổ biến động, tạo phiếu nhập kho, xem đơn hàng, xuất kho đơn đã thanh toán |
| **Admin** | Như trên + quản lý danh mục, sản phẩm, SKU, giá, kho, điều chỉnh tồn kho |

- Mỗi tài khoản có đúng một vai trò.
- Customer tự đăng ký. Tài khoản Staff và Admin chỉ được tạo bằng dữ liệu mẫu (seed).

---

## 4. Tech stack

| Mảng | Công nghệ | Ghi chú |
|---|---|---|
| Runtime | .NET 10 (LTS), C# | |
| API | ASP.NET Core dùng controller | Controller mỏng, xem `Rule.md` §2 |
| Truy cập dữ liệu | EF Core 10 + Npgsql | `ExecuteUpdateAsync` để cập nhật tồn kho nguyên tử |
| Database | PostgreSQL 17 trở lên | Extension `pg_trgm` cho tìm kiếm |
| Kiểm tra đầu vào | FluentValidation | Bắt buộc kiểm tra ở backend |
| Xác thực | ASP.NET Core Identity, cookie | Cookie HttpOnly, Secure, SameSite; không lưu JWT trên trình duyệt |
| Lỗi | ProblemDetails | Một định dạng lỗi thống nhất |
| Tài liệu API | Microsoft.AspNetCore.OpenApi + Scalar | |
| Cache | HybridCache (trong bộ nhớ, một instance) | Không dùng Redis (ngoài phạm vi) |
| Rate limiting | Rate limiter có sẵn của ASP.NET Core | |
| Job nền | `BackgroundService` + `PeriodicTimer` | Giải phóng hàng giữ quá hạn |
| Frontend | React + TypeScript + Vite | Hai app: `storefront`, `admin` |
| Thư viện frontend | React Router, TanStack Query, Tailwind CSS, Zod | Zod chỉ dùng cho form |
| Kiểu dữ liệu API | openapi-typescript | Sinh kiểu từ OpenAPI của backend |
| Kiểm thử | xUnit, Testcontainers (PostgreSQL), Bogus | Integration test dùng PostgreSQL thật |
| Load test | k6 | |
| Chạy cục bộ | Docker Compose | |
| CI | GitHub Actions | Build + test + lint mỗi lần push/PR |
| Deploy | VPS + Docker Compose + Caddy (HTTPS) | Dịch vụ rẻ/miễn phí tương đương cũng được |
| AI (điểm cộng) | Microsoft.Extensions.AI | Ollama khi dev, mô hình hosted khi demo |
| Theo dõi task | GitHub Issues + milestone, `gh` CLI | spec nằm trong issue (§17) |
| Quy trình với agent | Claude Code + mattpocock/skills | quy trình ở `Rule.md` §1 |

Không thêm thư viện ngoài bảng này khi chưa được người dùng đồng ý (xem `Rule.md` §7).

---

## 5. Kiến trúc

### 5.1 Kiểu kiến trúc

**Modular monolith**: một project ASP.NET Core (`src/Api`), một database PostgreSQL, một `AppDbContext`,
code chia thành các module. Cấu trúc thư mục được quy định trong `Rule.md` §2.

```
 Trình duyệt (storefront)   Trình duyệt (admin)
          \                  /
           \   HTTPS /api   /
            v              v
        ┌───────────────────────────┐
        │        ASP.NET Core API    │
        │  Identity  Catalog         │
        │  Inventory Ordering Payment│
        │  Job nền (hết hạn giữ hàng)│
        └─────────────┬─────────────┘
                      │
                 PostgreSQL
```

Khi chạy production, Caddy phục vụ cả hai app React và chuyển tiếp `/api` tới API trên **cùng một tên miền**,
nên cookie hoạt động mà không cần cấu hình cross-site. Khi dev, proxy của Vite dev server làm việc tương tự.

### 5.2 Các module

| Module | Quản lý | Service công khai cho module khác dùng |
|---|---|---|
| **Identity** | Người dùng, vai trò, đăng nhập | `ICurrentUser` (id, vai trò) |
| **Catalog** | Danh mục, sản phẩm, SKU, giá, tìm kiếm | `ICatalogService.GetSkusForCheckoutAsync(ids)` |
| **Inventory** | Kho, tồn kho, sổ biến động, giữ hàng | `IInventoryService`: `TryReserveAsync`, `ReleaseAsync`, `CommitAsync`, `ShipAsync`, `GetStockStatusAsync` |
| **Ordering** | Giỏ hàng, đơn hàng, dòng đơn, máy trạng thái đơn | `IOrderingService.MarkPaidAsync`, `GetOrderForPaymentAsync` |
| **Payment** | Thanh toán, xử lý webhook, cổng thanh toán giả lập | — |

### 5.3 Quy tắc phụ thuộc

```
Ordering ──► Catalog     (giá, thông tin SKU)
Ordering ──► Inventory   (giữ / trả / xuất hàng)
Payment  ──► Ordering    (đánh dấu đơn đã thanh toán)
```

- Không có phụ thuộc vòng. **Inventory không bao giờ phụ thuộc Ordering**; nó chỉ lưu tham chiếu (`ReferenceType`, `ReferenceId`).
- Các module chỉ gọi nhau qua các service interface công khai ở trên (`Rule.md` §2).
- Vì các module dùng chung một `AppDbContext`, service gọi có thể mở transaction rồi gọi service của module khác
  bên trong (ví dụ Ordering tạo đơn và Inventory giữ hàng trong **cùng một** transaction).
  Service nào mở transaction thì service đó chịu trách nhiệm về transaction.
- **Không dùng navigation property của EF giữa các module.** Entity chỉ tham chiếu dữ liệu module khác bằng id
  (ví dụ `OrderLine.SkuId`, không phải `OrderLine.Sku`). Nhờ vậy ranh giới module được giữ thật sự.
- **Cổng thanh toán giả lập đóng vai một hệ thống bên ngoài**: nó chỉ giao tiếp với module Payment qua HTTP
  (webhook), không bao giờ gọi trực tiếp service hay database.

---

## 6. Mô hình dữ liệu

Quy ước cho mọi bảng:
- Khóa chính: `Guid` sinh bằng `Guid.CreateVersion7()` (sắp xếp được, khó đoán).
- Thời gian: `timestamptz`, luôn là UTC.
- Tiền: `numeric(18,2)` ↔ `decimal` trong C#. Chỉ dùng VND.
- Bảng nào cũng có `CreatedAt`; bảng có thể sửa thì có thêm `UpdatedAt`.
- Tên bảng, tên cột: snake_case trong database (cấu hình bằng naming convention của EF), PascalCase trong C#.

### 6.1 Catalog

**categories**
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| Name | text | ví dụ "Điện thoại" |
| Slug | text | duy nhất, ví dụ `dien-thoai` |
| ParentId | uuid? | FK → categories, tối đa 2 cấp |
| SortOrder | int | |

**products**
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| CategoryId | uuid | FK |
| Name | text | ví dụ "Điện thoại Nova X 5G" |
| NameNormalized | text | chữ thường, bỏ dấu, `đ`→`d`; C# tính khi lưu; có GIN trigram index |
| Slug | text | duy nhất |
| Brand | text | thương hiệu hư cấu |
| Description | text | |
| Specs | jsonb | thông số theo danh mục, ví dụ `{"cpu": "...", "ramGb": 16}` |
| MinPrice | numeric(18,2) | giá thấp nhất trong các SKU đang hoạt động; cập nhật trong cùng transaction với mọi thay đổi SKU; dùng để lọc/sắp xếp theo giá |
| IsActive | bool | sản phẩm không hoạt động bị ẩn khỏi website |

**skus** (biến thể bán được)
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| ProductId | uuid | FK |
| SkuCode | text | duy nhất, ví dụ `NOVAX-128-BLK` |
| VariantName | text | ví dụ "128GB - Đen" |
| Price | numeric(18,2) | giá hiện tại, > 0 |
| Attributes | jsonb | ví dụ `{"storage": "128GB", "color": "Đen"}` |
| IsActive | bool | |

### 6.2 Inventory

**warehouses**
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| Code | text | duy nhất, ví dụ `HN01`, `HCM01`, `DN01` |
| Name | text | |
| City | text | dùng để chọn kho giao hàng |
| IsActive | bool | |

**stock_items** — mỗi dòng ứng với một cặp (SKU, kho)
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| SkuId | uuid | FK |
| WarehouseId | uuid | FK |
| OnHand | int | số lượng thực tế đang nằm trong kho |
| Reserved | int | số lượng đang giữ cho đơn chưa xuất kho |

Ràng buộc: `UNIQUE (SkuId, WarehouseId)`, `CHECK (OnHand >= 0)`, `CHECK (Reserved >= 0)`,
`CHECK (Reserved <= OnHand)`.
`Available = OnHand - Reserved` **chỉ được tính, không bao giờ lưu thành cột**.
Các ràng buộc CHECK là lớp bảo vệ cuối cùng; code không được dựa vào chúng cho luồng xử lý bình thường.

**stock_movements** — sổ biến động (chỉ được thêm: không UPDATE, không DELETE)
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| SkuId, WarehouseId | uuid | |
| Type | text | `Receipt` (nhập), `Shipment` (xuất), `Adjustment` (điều chỉnh) |
| QuantityDelta | int | thay đổi của `OnHand` (+ hoặc −, không bao giờ bằng 0) |
| OnHandAfter | int | `OnHand` ngay sau biến động này |
| ReferenceType | text? | ví dụ `Order`, `GoodsReceipt` |
| ReferenceId | uuid? | |
| Reason | text? | bắt buộc với `Adjustment` |
| CreatedBy | uuid | id người thực hiện |
| CreatedAt | timestamptz | |

Index: `(SkuId, WarehouseId, CreatedAt, Id)`.
**Bất biến:** với mỗi dòng tồn kho, `SUM(QuantityDelta) == OnHand`.
Việc giữ hàng **không** tạo dòng trong sổ (nó thay đổi `Reserved`, không thay đổi `OnHand`).

**stock_reservations**
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderId | uuid | chỉ là tham chiếu (không có FK sang bảng của Ordering) |
| SkuId, WarehouseId | uuid | |
| Quantity | int | > 0 |
| Status | text | `Active`, `Committed`, `Released`, `Shipped` |

Hạn thanh toán nằm ở đơn hàng (`orders.ExpiresAt`), không nằm ở reservation.

### 6.3 Ordering

**carts**: `Id`, `CustomerId` (duy nhất), `UpdatedAt`
**cart_items**: `CartId`, `SkuId`, `Quantity` (1–5); khóa chính `(CartId, SkuId)`

**orders**
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderNumber | text | duy nhất, dễ đọc, ví dụ `GN-20261121-000123` |
| CustomerId | uuid | chủ đơn |
| Status | text | xem §7.3 |
| WarehouseId | uuid | kho giao hàng |
| Subtotal, Total | numeric(18,2) | Total = Subtotal (không có phí ship/giảm giá trong phạm vi) |
| IdempotencyKey | text | `UNIQUE (CustomerId, IdempotencyKey)` |
| RequestHash | text | SHA-256 của request đặt hàng đã chuẩn hóa (§7.7) |
| ShippingName, ShippingPhone, ShippingAddress, ShippingCity | text | |
| ExpiresAt | timestamptz | hạn thanh toán |
| PaidAt, ShippedAt, CompletedAt, CancelledAt | timestamptz? | |
| CancelReason | text? | `Customer`, `Expired` |

**order_lines**
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderId | uuid | FK |
| SkuId | uuid | |
| ProductNameSnapshot, VariantNameSnapshot, SkuCodeSnapshot | text | sao chép lúc đặt hàng |
| UnitPrice | numeric(18,2) | sao chép lúc đặt hàng |
| Quantity | int | |
| LineTotal | numeric(18,2) | `UnitPrice * Quantity` |

### 6.4 Payment

**payments**
| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderId | uuid | |
| Provider | text | `Mock` |
| ProviderTransactionId | text? | **duy nhất** — dùng để loại webhook trùng |
| Amount | numeric(18,2) | |
| Status | text | `Pending`, `Succeeded`, `Failed`, `RefundRequired`, `Refunded` |
| CreatedAt, UpdatedAt | timestamptz | |

### 6.5 Identity

Các bảng của ASP.NET Core Identity (users, roles, user roles). Thêm các trường cho user: `FullName`, `Phone`, `City`.
Thêm bảng `data_protection_keys` — khóa Data Protection của ASP.NET Core (dùng để mã hóa cookie đăng nhập) được lưu
trong database, để mỗi lần deploy lại container không làm mọi người dùng bị đăng xuất (§11.5).

### 6.6 Index, sequence và giá trị tự sinh

| Bảng | Index / ràng buộc | Dùng cho |
|---|---|---|
| products | unique `Slug`; `(CategoryId)`; `(MinPrice)`; GIN trigram trên `NameNormalized` | danh sách, tìm kiếm |
| skus | unique `SkuCode`; `(ProductId)` | chi tiết sản phẩm, đặt hàng |
| stock_items | unique `(SkuId, WarehouseId)`; `(WarehouseId)` | giữ hàng, tổng quan tồn kho |
| stock_movements | `(SkuId, WarehouseId, CreatedAt, Id)` | trang sổ biến động (keyset) |
| stock_reservations | `(OrderId)` | trả / cam kết / xuất hàng |
| orders | unique `OrderNumber`; unique `(CustomerId, IdempotencyKey)`; `(CustomerId, CreatedAt DESC)`; `(Status, CreatedAt)`; partial `(ExpiresAt) WHERE Status = 'PendingPayment'` | đơn của tôi, danh sách admin, **job hết hạn** |
| order_lines | `(OrderId)` | chi tiết đơn |
| payments | unique `ProviderTransactionId`; `(OrderId)` | loại webhook trùng |

- **Mã đơn hàng:** dùng sequence `order_number_seq` của PostgreSQL (an toàn khi nhiều request đồng thời).
  Định dạng `GN-{yyyyMMdd}-{seq:D6}`, ngày theo giờ Việt Nam (`Asia/Ho_Chi_Minh`). Sequence không reset; có thể bị nhảy số.
- **Nguồn thời gian:** chỉ dùng `TimeProvider` của ứng dụng (không dùng `now()` của database),
  để test điều khiển được thời gian.

---

## 7. Quy tắc nghiệp vụ

### 7.1 Số lượng tồn kho

- `OnHand`: số lượng thực tế trong kho.
- `Reserved`: số lượng đã hứa cho các đơn chưa xuất kho.
- `Available = OnHand - Reserved`: số lượng còn bán được.
- Khách hàng không bao giờ thấy con số chính xác. Trạng thái hiển thị cho khách:
  - `InStock` (Còn hàng): tổng số còn bán được ở các kho đang hoạt động > 5
  - `LowStock` (Sắp hết): 1–5
  - `OutOfStock` (Hết hàng): 0
- Staff và Admin thấy số chính xác theo từng kho.
- Trạng thái hàng là tổng của mọi kho, nhưng mỗi đơn chỉ lấy hàng từ **một** kho (§7.4).
  Vì vậy đặt hàng vẫn có thể bị `409` dù trang sản phẩm hiện "Còn hàng"; giao diện phải xử lý trường hợp này.

### 7.2 Giữ hàng (quy tắc quan trọng nhất)

Giữ `q` sản phẩm của một SKU tại một kho là **một câu lệnh SQL cập nhật có điều kiện, nguyên tử**:

```csharp
var affected = await db.StockItems
    .Where(s => s.SkuId == skuId
             && s.WarehouseId == warehouseId
             && s.OnHand - s.Reserved >= quantity)
    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Reserved, x => x.Reserved + quantity), ct);
// affected == 1 → giữ được hàng; affected == 0 → không đủ hàng
```

- Không bao giờ đọc tồn kho lên, kiểm tra trong C#, rồi mới lưu.
- Đơn có nhiều dòng thì giữ hàng cho tất cả các dòng **trong một transaction**; một dòng thất bại thì hoàn tác toàn bộ.
- Giữ hàng theo thứ tự **sắp xếp theo `SkuId`** để thứ tự khóa luôn nhất quán, tránh deadlock.

### 7.3 Vòng đời đơn hàng

```
                ┌──────────► Cancelled (khách hủy)
                │
PendingPayment ─┼──────────► Expired   (quá hạn thanh toán)
                │
                └─► Paid ──► Shipped ──► Completed
```

| Chuyển trạng thái | Ai thực hiện | Ảnh hưởng tồn kho |
|---|---|---|
| (mới) → `PendingPayment` | Khách đặt hàng | `Reserved += q`; reservation `Active` |
| `PendingPayment` → `Paid` | Webhook thanh toán | reservation `Committed` (vẫn giữ, không còn hạn) |
| `PendingPayment` → `Cancelled` | Khách | `Reserved -= q`; reservation `Released` |
| `PendingPayment` → `Expired` | Job hết hạn | `Reserved -= q`; reservation `Released` |
| `Paid` → `Shipped` | WarehouseStaff | `OnHand -= q`, `Reserved -= q`; sổ ghi `Shipment`; reservation `Shipped` |
| `Shipped` → `Completed` | WarehouseStaff (giả lập giao thành công) | không |

- Mọi chuyển trạng thái khác đều bị từ chối (`409 Conflict`).
- Đổi trạng thái bằng câu lệnh cập nhật có điều kiện trên trạng thái hiện tại
  (`... WHERE Id = @id AND Status = 'PendingPayment'`), nên hai hành động tranh nhau
  (ví dụ thanh toán và hết hạn) không bao giờ cùng thắng.
- Hủy đơn đã `Paid` nằm ngoài phạm vi.

### 7.4 Chọn kho giao hàng (không tách đơn)

1. Lập danh sách kho ứng viên: kho đang hoạt động, ưu tiên kho có `City == ShippingCity`, sau đó theo `Code`.
2. Với từng kho ứng viên, thử giữ hàng cho **tất cả** các dòng trong một transaction.
3. Kho đầu tiên giữ được hàng sẽ giao toàn bộ đơn.
4. Không kho nào đáp ứng được cả đơn → `409 Conflict`, kèm danh sách SKU bị thiếu.

### 7.5 Giá

- Server đọc giá SKU hiện tại khi đặt hàng. Giá do client gửi lên bị bỏ qua.
- Giá, tên và mã SKU được sao chép vào `order_lines`. Đổi giá sau đó không ảnh hưởng đơn đã có.
- `LineTotal = UnitPrice * Quantity`, `Total = Σ LineTotal`. VND thực tế không có phần lẻ nhưng vẫn dùng `decimal`.
- Không đặt được sản phẩm/SKU đang ở trạng thái không hoạt động.

### 7.6 Giỏ hàng

- Mỗi khách một giỏ. Số lượng mỗi dòng: 1–5.
- Thêm vào giỏ **không** giữ hàng.
- Giỏ hiển thị giá và trạng thái hàng hiện tại; lúc đặt hàng mọi thứ được kiểm tra lại.
- Đặt hàng thành công thì xóa các sản phẩm đã đặt khỏi giỏ **trong cùng transaction**.
- **Giới hạn: mỗi khách tối đa 3 đơn `PendingPayment`.** Lần đặt thứ 4 trả về `409 too_many_pending_orders`.
  Nếu không giới hạn, một người có thể khóa hết hàng bằng cách đặt nhiều đơn rồi không thanh toán.

### 7.7 Idempotency khi đặt hàng

- `POST /api/orders` bắt buộc có header `Idempotency-Key` (UUID do client sinh, mỗi lần bấm đặt hàng một key).
- Ràng buộc duy nhất `(CustomerId, IdempotencyKey)`.
- `RequestHash` = SHA-256 của request đã chuẩn hóa (các dòng giỏ sắp xếp theo `SkuId` + thông tin giao hàng).
- Cùng key + cùng hash → trả về **đơn đã có** với cùng phản hồi (`200`), không tạo đơn mới.
- Cùng key + hash **khác** → `409 idempotency_key_reused`.
- Lần thử thất bại (ví dụ hết hàng) bị hoàn tác và không lưu gì, nên thử lại với cùng key sẽ chạy lại từ đầu.
- Hai request đồng thời cùng key → một request insert được, request còn lại gặp lỗi vi phạm ràng buộc duy nhất.
  Trong PostgreSQL, lỗi này **làm hỏng transaction**: phải rollback, rồi đọc đơn đã có bằng một truy vấn mới.

### 7.8 Hết hạn giữ hàng

- Hạn thanh toán: `ExpiresAt = CreatedAt + 15 phút` (cấu hình: `Ordering:PaymentTimeoutMinutes`).
- Một `BackgroundService` chạy mỗi 60 giây, xử lý các đơn `PendingPayment` quá hạn theo lô 100 đơn.
- Với mỗi đơn: cập nhật có điều kiện sang `Expired`; chỉ khi thành công mới trả lại hàng đã giữ.
- Job phải an toàn nếu sau này chạy trên nhiều instance: cập nhật có điều kiện theo từng đơn đảm bảo điều đó.
- Job dừng gọn gàng khi ứng dụng tắt (`CancellationToken`); mỗi đơn là một transaction riêng nên lô xử lý dở vẫn an toàn.
- Phản hồi đơn hàng có cả `ExpiresAt` **và** `ServerNow`; đồng hồ đếm ngược ở frontend dùng hiệu của hai giá trị này,
  nên đồng hồ trên máy khách bị sai cũng không ảnh hưởng.

### 7.9 Thanh toán (giả lập)

- Khách bắt đầu thanh toán → tạo payment `Pending` → chuyển khách tới trang thanh toán giả lập.
- Cổng giả lập gửi webhook tới `POST /api/payments/webhook`:
  - Body: `providerTransactionId`, `orderId`, `amount`, `status` (`Succeeded` / `Failed`).
  - Header `X-Timestamp`: thời điểm gửi webhook, tính bằng giây Unix.
  - Header `X-Signature`: HMAC-SHA256 của `"{timestamp}.{rawBody}"`, dùng khóa bí mật chung (lấy từ cấu hình/secret).
- Cổng giả lập gửi webhook từ một **worker chạy nền** (`Channel` trong bộ nhớ), không gửi ngay trong request,
  và gửi lại tối đa 3 lần khi nhận phản hồi không phải 2xx (sau 1 giây, 5 giây, 25 giây) — giống cổng thanh toán thật.
  Đây chính là lý do webhook phải idempotent.
- Storefront biết kết quả bằng cách **hỏi lại liên tục (polling)** `GET /api/orders/{id}` mỗi 2 giây, tối đa 60 giây
  (không dùng WebSocket/SignalR trong phạm vi này).
- Xử lý webhook:
  1. Kiểm tra chữ ký bằng phép so sánh thời gian cố định (constant-time), và từ chối timestamp cũ hơn 5 phút
     (chống gửi lại webhook cũ) → `401`, ghi log cảnh báo.
  2. Loại trùng theo `ProviderTransactionId` (duy nhất) → đã xử lý rồi: trả `200`, không làm gì thêm.
  3. Số tiền phải bằng tổng đơn → không bằng: đánh dấu payment `Failed`, ghi log cảnh báo.
  4. `Succeeded` và đơn vẫn `PendingPayment` → đơn chuyển `Paid`, reservation `Committed`.
  5. `Succeeded` nhưng đơn đã `Expired`/`Cancelled` → payment `RefundRequired`, rồi hoàn tiền giả lập → `Refunded`.
  6. `Failed` → payment `Failed`; đơn vẫn `PendingPayment` đến khi hết hạn (khách có thể thử lại).
- Endpoint cổng giả lập (`/api/mock-gateway/...`) **chỉ** tồn tại ở môi trường `Development` và `Demo`.
  Nó giả lập được: thành công, thất bại, gửi webhook trùng (gửi hai lần), gửi webhook trễ.
  Người gọi phải là chủ của payment (khách không thể "thanh toán hộ" đơn của người khác, kể cả ở môi trường Demo).

### 7.10 Nghiệp vụ kho

- **Nhập kho** (WarehouseStaff, Admin): thêm số lượng cho một hoặc nhiều SKU vào một kho.
  `OnHand += q` và mỗi dòng có một bản ghi `Receipt` trong sổ, tất cả trong một transaction.
- **Điều chỉnh** (chỉ Admin): sửa tăng hoặc giảm, bắt buộc có lý do (kiểm kê, hàng hỏng...).
  Điều chỉnh giảm không được làm `OnHand < Reserved`.
- **Xuất kho** (WarehouseStaff): chỉ với đơn `Paid`; xem §7.3.

### 7.11 Phân quyền truy cập

- Các endpoint của khách luôn lọc theo id người dùng lấy từ cookie đăng nhập.
- Truy cập đơn của khách khác trả về `404` (không phải `403`, để không lộ việc đơn đó tồn tại).
- Các endpoint của Admin/Staff nằm dưới `/api/admin/...` và được bảo vệ bằng policy theo vai trò.

---

## 8. Các luồng chính (từng bước)

### 8.1 Đặt hàng — `POST /api/orders`

**Trước transaction** (giữ transaction thật ngắn — dòng tồn kho đã giữ bị khóa cho tới khi commit):

1. Bắt buộc đăng nhập (Customer), có header CSRF (§11.5) và `Idempotency-Key`.
2. Kiểm tra đầu vào (thông tin giao hàng, giỏ có ít nhất một dòng). Tính `RequestHash`.
3. Nếu khách này đã có đơn với key đó → cùng hash: trả về đơn đó; khác hash: `409 idempotency_key_reused`.
4. Nếu khách đang có 3 đơn `PendingPayment` → `409 too_many_pending_orders`.
5. Lấy các dòng trong giỏ; lấy SKU từ Catalog (chỉ SKU đang hoạt động, giá hiện tại). Tính tiền từng dòng và tổng tiền.
6. Lập danh sách kho ứng viên (§7.4).

**Với mỗi kho ứng viên, một transaction:**

7. Giữ hàng cho mọi dòng, theo thứ tự `SkuId` (§7.2). Một dòng thất bại → rollback, thử kho tiếp theo.
8. Lấy mã đơn tiếp theo; thêm đơn (`PendingPayment`, `ExpiresAt`), các dòng đơn (bản sao giá/tên), các reservation (`Active`).
9. Xóa các sản phẩm đã đặt khỏi giỏ.
10. Commit. Nếu gặp deadlock hoặc lỗi serialization, chạy lại toàn bộ transaction (§11.3).

**Sau đó:**

11. Trả `201` kèm id đơn, mã đơn, tổng tiền, `ExpiresAt`, `ServerNow`.
12. Lỗi: không kho nào đáp ứng được → `409 out_of_stock` kèm các SKU bị thiếu; đầu vào sai → `400`; chưa đăng nhập → `401`.

### 8.2 Webhook thanh toán — `POST /api/payments/webhook`

Endpoint không cần đăng nhập, được bảo vệ bằng chữ ký. Các bước ở §7.9. Luôn xử lý nhanh; không gọi dịch vụ ngoài trong transaction.

### 8.3 Job hết hạn

Mỗi 60 giây: lấy tối đa 100 id đơn có `Status = PendingPayment AND ExpiresAt < now` →
với mỗi đơn: transaction { cập nhật trạng thái có điều kiện → nếu được 1 dòng: trả hàng (`Reserved -= q`, reservation `Released`) }.
Ghi log số đơn hết hạn ở mức `Information`.

### 8.4 Nhập kho — `POST /api/admin/stock/receipts`

Kiểm tra các dòng → transaction { với mỗi dòng: tạo hoặc cập nhật dòng tồn kho, `OnHand += q`, thêm biến động `Receipt` kèm `OnHandAfter` } → `201`.

### 8.5 Xuất kho — `POST /api/admin/orders/{id}/ship`

Transaction { cập nhật trạng thái có điều kiện `Paid → Shipped` → với mỗi reservation: `OnHand -= q`, `Reserved -= q`,
thêm biến động `Shipment`, reservation `Shipped` } → `200`.

### 8.6 Khách hủy đơn — `POST /api/orders/{id}/cancel`

Chỉ chủ đơn. Transaction { cập nhật có điều kiện `PendingPayment → Cancelled` → trả hàng đã giữ } → `200`.
Trạng thái khác → `409`.

---

## 9. Danh sách API

Mọi route có tiền tố `/api`. Mọi endpoint trả danh sách đều phân trang (`Rule.md` §3).

| Method | Route | Vai trò | Ghi chú |
|---|---|---|---|
| POST | `/auth/register` | Khách vãng lai | có rate limit |
| POST | `/auth/login` | Khách vãng lai | có rate limit |
| POST | `/auth/logout` | Đã đăng nhập | |
| GET | `/auth/me` | Đã đăng nhập | id, tên, vai trò |
| GET | `/categories` | Khách vãng lai | có cache |
| GET | `/products` | Khách vãng lai | `q`, `categoryId`, `brand`, `minPrice`, `maxPrice`, `sort`, `page`, `pageSize`; có cache |
| GET | `/products/{slug}` | Khách vãng lai | sản phẩm + SKU; cache 60 giây |
| GET | `/products/{slug}/stock-status` | Khách vãng lai | trạng thái hàng theo từng SKU; cache ≤ 10 giây |
| GET | `/cart` | Customer | |
| PUT | `/cart/items/{skuId}` | Customer | body `{ quantity }` |
| DELETE | `/cart/items/{skuId}` | Customer | |
| POST | `/orders` | Customer | header `Idempotency-Key`; có rate limit |
| GET | `/orders` | Customer | chỉ đơn của mình |
| GET | `/orders/{id}` | Customer | chỉ đơn của mình, nếu không → `404` |
| POST | `/orders/{id}/cancel` | Customer | đơn của mình, chỉ khi `PendingPayment` |
| POST | `/orders/{id}/payments` | Customer | tạo payment giả lập, trả về URL trang thanh toán |
| POST | `/payments/webhook` | Không đăng nhập + chữ ký | |
| POST | `/mock-gateway/payments/{paymentId}/simulate` | Chỉ Dev/Demo | `{ outcome, duplicate, delaySeconds }` |
| GET/POST/PUT | `/admin/categories[...]` | Admin | |
| GET/POST/PUT | `/admin/products[...]`, `/admin/products/{id}/skus[...]` | Admin | xóa cache danh mục sản phẩm |
| GET/POST/PUT | `/admin/warehouses[...]` | Admin (GET: cả Staff) | |
| GET | `/admin/stock` | Staff, Admin | lọc theo kho, SKU, sắp hết hàng |
| POST | `/admin/stock/receipts` | Staff, Admin | |
| POST | `/admin/stock/adjustments` | Admin | bắt buộc có lý do |
| GET | `/admin/stock/movements` | Staff, Admin | phân trang keyset (`cursor`) |
| GET | `/admin/orders` | Staff, Admin | lọc theo trạng thái |
| POST | `/admin/orders/{id}/ship` | Staff | chỉ đơn `Paid` |
| POST | `/admin/orders/{id}/complete` | Staff | chỉ đơn `Shipped` |
| GET | `/health/live` | Khách vãng lai | tiến trình đang chạy (Docker healthcheck dùng) |
| GET | `/health/ready` | Khách vãng lai | kết nối được database (công cụ giám sát và bước kiểm tra khi deploy dùng) |
| POST | `/assistant/chat` | Customer | **điểm cộng B1**, có rate limit, có feature flag |

Mã trạng thái: `200`/`201` thành công, `400` dữ liệu không hợp lệ, `401` chưa đăng nhập, `403` sai vai trò,
`404` không tìm thấy hoặc không phải của mình, `409` xung đột nghiệp vụ (hết hàng, sai trạng thái), `429` bị giới hạn tần suất.
Mọi lỗi dùng ProblemDetails, có `code` cố định và `traceId` của request.
Các mã lỗi nghiệp vụ: `out_of_stock`, `invalid_status`, `too_many_pending_orders`, `idempotency_key_reused`.

Định dạng phản hồi:
- Danh sách phân trang theo trang: `{ items, page, pageSize, totalCount }` (`pageSize` mặc định 20, tối đa 100).
- Danh sách phân trang keyset: `{ items, nextCursor }` (`nextCursor` là `null` ở trang cuối).
- Không đánh version API (chỉ có một client do mình kiểm soát). Thay đổi phá vỡ tương thích phải cập nhật kiểu dữ liệu sinh ra cho frontend trong cùng PR.

---

## 10. Thiết kế tìm kiếm (P6)

- `NameNormalized` được C# tính mỗi lần lưu sản phẩm: chuyển chữ thường → bỏ dấu
  (chuẩn hóa Unicode FormD + bỏ các dấu kết hợp) → đổi `đ` thành `d` → gộp khoảng trắng.
- Cùng hàm đó được dùng để chuẩn hóa từ khóa người dùng gõ.
- Truy vấn: `NameNormalized ILIKE '%term%'` (hoặc so độ tương tự `%`), dựa trên **GIN trigram index** (`pg_trgm`).
- Bộ lọc: danh mục (gồm cả danh mục con), thương hiệu, khoảng giá (giá SKU thấp nhất). Sắp xếp: liên quan nhất, giá tăng/giảm, mới nhất.
- Hàm chuẩn hóa có unit test với các trường hợp tiếng Việt (`Điện thoại` → `dien thoai`, `Đồng hồ` → `dong ho`).

---

## 11. Thiết kế phi chức năng

### 11.1 Mục tiêu và ước tính quy mô

Đây là **mục tiêu cần kiểm chứng**, không phải sự thật. Sau lần chạy k6 đầu tiên, điều chỉnh lại và ghi vào §19.

| Mảng | Mục tiêu |
|---|---|
| Hạ tầng | 1 VPS (2 vCPU / 4 GB RAM), 1 instance API, 1 PostgreSQL |
| Đọc danh mục sản phẩm | p95 < 200 ms ở 200 request/giây (có cache) |
| Đặt hàng | p95 < 500 ms ở 50 request/giây, tồn kho bình thường |
| Flash buy (L2) | 1.000 người mua đồng thời, 1 sản phẩm → 1 thành công, không có lỗi nào ngoài `409`, mọi phản hồi trong 10 giây |
| Tính đúng đắn | 0 sản phẩm bị bán vượt và bất biến của sổ biến động luôn đúng, trong mọi bài test |
| Tính sẵn sàng | Một server, không có dự phòng (chấp nhận được với bản demo); container tự khởi động lại khi lỗi |
| Mức mất dữ liệu chấp nhận được (RPO) | tối đa 24 giờ (backup hằng ngày) |
| Thời gian khôi phục (RTO) | < 1 giờ (khôi phục từ bản backup gần nhất) |

Ước tính nhanh:
- 100.000 đơn × khoảng 2 dòng ≈ 300.000 dòng, khoảng 100 MB kể cả index. Sổ biến động tăng với tốc độ tương tự.
  → Một PostgreSQL là quá đủ; không cần chia nhỏ (sharding) hay phân vùng (partitioning).
- Kết nối: pool của API `Maximum Pool Size=50`, PostgreSQL `max_connections=100`
  (chừa chỗ cho migration, backup, công cụ quản trị). Thời gian chờ pool `Timeout=15` giây.
- Vì vậy 1.000 request đồng thời sẽ **xếp hàng chờ kết nối** — đây là điều bình thường; mục tiêu 10 giây đã tính đến việc này.

### 11.2 Hiệu năng (P7)

- **Giữ transaction thật ngắn.** Kiểm tra đầu vào, tính giá và chọn kho ứng viên **trước** khi mở transaction đặt hàng
  (§8.1). Dòng tồn kho đã giữ bị khóa cho tới khi commit, nên mỗi mili giây thừa trong transaction đều làm chậm
  mọi người đang mua cùng SKU.
- **Cache danh mục sản phẩm** bằng HybridCache: danh mục, các trang danh sách sản phẩm, chi tiết sản phẩm. Hết hạn sau 60 giây.
  Khi sửa sản phẩm/SKU/giá thì xóa các key bị ảnh hưởng. Trạng thái hàng có endpoint riêng và cache ngắn (≤ 10 giây).
  Danh sách sản phẩm không hiển thị trạng thái hàng (để việc cache danh sách đơn giản).
- **Không bao giờ** dùng tồn kho trong cache để quyết định giữ hàng.
- **Các policy rate limiting:**
  - `auth`: 5 request/phút mỗi IP (đăng nhập, đăng ký)
  - `checkout`: 10 request/phút mỗi người dùng
  - `assistant`: 20 request/giờ mỗi người dùng (điểm cộng)
  - toàn cục: 100 request/10 giây mỗi IP
  - Request bị từ chối trả `429` kèm `Retry-After`.
- **Truy vấn:** `AsNoTracking`, projection bằng `Select`, phân trang, index cho các cột hay lọc.
- **Đo bằng k6** (script trong `tests/load/`):
  - `L1-catalog-spike.js`: tăng dần tới N người dùng ảo xem sản phẩm; chạy **trước** và **sau** khi thêm cache.
  - `L2-flash-buy.js`: 1.000 người dùng ảo cùng mua một SKU chỉ còn 1 sản phẩm.
  - Ghi p95 latency, tỷ lệ lỗi và cấu hình máy vào `README.md`. Chỉ ghi số liệu đo thật.
  - Chạy k6 ở môi trường `LoadTest` (§11.6): tắt rate limiting (nếu không, mọi request của k6 dùng chung một IP
    và đều bị `429`), có sẵn 1.000 tài khoản khách để test tải, đăng nhập trong bước `setup()` của k6.
  - Chạy k6 từ **một máy khác** với server; nếu chạy chung, k6 chiếm CPU của server và số liệu sẽ sai.
  - Kiểm tra rate limiter riêng (integration test + một script k6 nhỏ mong đợi `429`).

### 11.3 Độ tin cậy và các tình huống lỗi

| Tình huống | Hành vi bắt buộc |
|---|---|
| API sập giữa lúc đặt hàng | Transaction chưa commit → PostgreSQL tự hoàn tác; không giữ hàng, không có đơn |
| Bấm hai lần / client gửi lại | Idempotency key (§7.7) |
| Webhook bị gửi trùng hoặc gửi lại | Loại trùng theo `ProviderTransactionId` (§7.9) |
| Webhook không bao giờ tới | Đơn hết hạn, hàng được trả lại; webhook tới muộn → `RefundRequired` |
| Job hết hạn bị lỗi / server khởi động lại | Mọi trạng thái nằm trong database; lần chạy sau tiếp tục bình thường |
| Tắt ứng dụng khi job đang chạy | Tôn trọng `CancellationToken`; mỗi đơn một transaction |
| Database không truy cập được | `/health/ready` báo lỗi; API trả `503` dạng ProblemDetails; ứng dụng vẫn khởi động được (không làm việc với DB lúc khởi động) |
| Deadlock hoặc lỗi serialization (`40P01`, `40001`) | Chạy lại toàn bộ transaction tối đa 3 lần, chờ một khoảng ngắn ngẫu nhiên (đặt hàng, xuất kho, hết hạn) |
| Đầy ổ cứng | Ngăn bằng xoay vòng log và giới hạn số bản backup (§11.6) |
| Deploy lỗi | Deploy lại image của phiên bản trước (§11.6) |

Quy tắc database:
- Dùng mức cô lập mặc định của PostgreSQL (`READ COMMITTED`); câu lệnh cập nhật có điều kiện nguyên tử vẫn đúng ở mức này.
- **Không** bật `EnableRetryOnFailure` của EF Core cho toàn bộ ứng dụng — nó xung đột với transaction mở thủ công.
  Chỉ thử lại ở những chỗ đã liệt kê ở trên.
- Sau bất kỳ lỗi database nào bên trong transaction (ví dụ vi phạm ràng buộc duy nhất), transaction đã hỏng:
  rollback, rồi chạy các truy vấn tiếp theo bên ngoài nó.

### 11.4 Khả năng quan sát (observability)

- Log có cấu trúc ghi ra stdout (`Rule.md` §6); Docker thu thập log.
- Mỗi request có một trace id, được ghi vào mọi dòng log và trả về dạng `traceId` trong mọi ProblemDetails,
  để khi người dùng báo lỗi có thể tìm đúng log.
- Ghi log các sự kiện nghiệp vụ: tạo / hết hạn / thanh toán / xuất kho đơn hàng, nhập / điều chỉnh kho, webhook bị từ chối.
- `/health/live` cho healthcheck của Docker (chỉ khởi động lại khi tiến trình bị treo, không khởi động lại khi DB lỗi);
  `/health/ready` cho công cụ giám sát và bước kiểm tra khi deploy.
- Một dịch vụ giám sát uptime bên ngoài (dịch vụ miễn phí nào cũng được) kiểm tra `/health/ready` mỗi 5 phút và báo cho bạn.
- Trace/metric bằng OpenTelemetry: tùy chọn, chỉ làm nếu còn thời gian (W5-02).

### 11.5 Thiết kế bảo mật

- **Cookie đăng nhập:** HttpOnly, Secure, `SameSite=Strict`, hết hạn trượt 7 ngày.
- **API không bao giờ chuyển hướng sang trang đăng nhập:** cấu hình sự kiện của cookie để trả `401` / `403`
  (mặc định ASP.NET Core sẽ chuyển hướng).
- **CSRF:** ngoài `SameSite=Strict`, mọi request thay đổi dữ liệu (POST/PUT/PATCH/DELETE) phải gửi header
  `X-CSRF: 1`; thiếu header thì trả `403`. Trình duyệt không thể thêm header tự đặt vào request từ trang web khác
  nếu không qua CORS preflight, mà ta không cho phép. Endpoint webhook được miễn (nó dùng chữ ký).
- **Khóa Data Protection** lưu trong PostgreSQL (`data_protection_keys`), để deploy lại không làm hỏng cookie.
- **Identity:** mật khẩu ≥ 8 ký tự; khóa tài khoản 15 phút sau 5 lần đăng nhập sai;
  thông báo lỗi chỉ nói "email hoặc mật khẩu không đúng", không nói sai cái nào.
- **Reverse proxy:** bật middleware Forwarded Headers, chỉ tin container Caddy,
  để dùng đúng IP thật của khách (cho rate limiting và log) và đúng giao thức HTTPS.
  Nếu không bật, mọi người dùng chung IP của Caddy và chung một giới hạn tần suất.
- **Security headers** (cấu hình ở Caddy): HSTS, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: strict-origin-when-cross-origin`, một Content-Security-Policy cơ bản.
- **Webhook:** chữ ký + timestamp + so sánh thời gian cố định (§7.9).
- **Secret:** user-secrets khi dev; biến môi trường từ file `.env` (quyền 600, không đưa vào git) trên VPS.
- **Thư viện phụ thuộc:** CI chạy `dotnet list package --vulnerable` và `npm audit --audit-level=high`; bật Dependabot.
- **Demo công khai:** bật rate limit; đặt giới hạn chi tiêu ở nhà cung cấp AI (điểm cộng B1).

### 11.6 Môi trường, deploy và vận hành

| Môi trường | Mục đích | Khác biệt |
|---|---|---|
| `Development` | làm việc trên máy | bật cổng thanh toán giả lập, dữ liệu demo, user-secrets |
| `Test` | integration test | Testcontainers, cơ chế xác thực dành cho test (§13.2) |
| `LoadTest` | chạy k6 | tắt rate limiting, seed 1.000 tài khoản khách để test tải |
| `Demo` | bản deploy công khai | bật cổng thanh toán giả lập, dữ liệu demo, hạn thanh toán 1 phút |

- Cấu hình dùng các lớp options được kiểm tra ngay khi khởi động (`ValidateOnStart`), thiếu cấu hình là báo lỗi ngay.
- **CI** (mỗi lần push/PR): build, test, lint, kiểm tra lỗ hổng thư viện.
- **Image:** khi merge vào `main`, build Docker image gắn tag bằng mã commit git và đẩy lên GitHub Container Registry.
- **Deploy** (kích hoạt thủ công bằng `workflow_dispatch`, qua SSH): backup DB → chạy **migration bundle** của EF Core →
  `docker compose pull && docker compose up -d` → chờ `/health/ready` báo ổn.
- **Migration** không bao giờ tự chạy khi ứng dụng khởi động. Migration phải tương thích ngược với phiên bản ứng dụng
  trước đó (thêm cột mới ở dạng nullable trước; chỉ xóa cột cũ ở lần phát hành sau), để có thể quay lui.
- **Quay lui:** deploy lại image của phiên bản trước. Khôi phục backup là phương án cuối cùng.
- **Backup:** `pg_dump` hằng ngày, giữ 7 ngày, sao lưu ra ngoài VPS. **Thử khôi phục một lần trước khi demo.**
- Dữ liệu PostgreSQL nằm trong một Docker volume có tên. Mọi container đặt `restart: unless-stopped`.
- Xoay vòng log của Docker: `max-size: 10m`, `max-file: 3`.

### 11.7 Giới hạn khi mở rộng (đã biết và chấp nhận)

Dự án giả định **chỉ có một instance API**. Những gì sẽ hỏng khi chạy từ 2 instance trở lên, và cách sửa:

| Thành phần | Vấn đề khi có nhiều instance | Cách sửa (giải thích, không làm) |
|---|---|---|
| Cache trong bộ nhớ | xóa cache ở instance này không ảnh hưởng instance khác | dùng Redis làm cache chung, hoặc thời gian hết hạn rất ngắn |
| Rate limiter | mỗi instance đếm riêng | rate limiter dựa trên Redis, hoặc giới hạn ở proxy/gateway |
| Job hết hạn | chạy trên mọi instance | vốn đã an toàn (cập nhật có điều kiện); thêm `FOR UPDATE SKIP LOCKED` để tránh làm trùng việc |
| Khóa Data Protection | — | đã dùng chung qua database |
| Hàng đợi của cổng giả lập | nằm trong bộ nhớ, mất khi khởi động lại | chấp nhận được với bản giả lập |

---

## 12. Frontend

### 12.1 Website bán hàng (`src/web/storefront`)

| Trang | Ghi chú |
|---|---|
| Trang chủ / danh sách sản phẩm | menu danh mục, ô tìm kiếm, bộ lọc, phân trang (không hiện trạng thái hàng) |
| Chi tiết sản phẩm | chọn SKU, giá, trạng thái hàng (request riêng), thêm vào giỏ |
| Giỏ hàng | số lượng, giá hiện tại, cảnh báo tồn kho |
| Thanh toán | form giao hàng (Zod), đặt hàng kèm idempotency key, khóa nút khi đang gửi; xử lý mọi mã lỗi `409` |
| Trang thanh toán giả lập | các nút: Thành công / Thất bại / Gửi webhook trùng / Gửi trễ; sau đó polling trạng thái đơn |
| Đơn của tôi, Chi tiết đơn | trạng thái, đếm ngược theo `ExpiresAt` − `ServerNow`, nút hủy |

Mọi request thay đổi dữ liệu đều gửi header `X-CSRF: 1` (cài một lần trong API client dùng chung).
| Đăng nhập / Đăng ký | |
| Khung chat | **điểm cộng B1** |

### 12.2 Trang quản trị (`src/web/admin`)

| Trang | Vai trò |
|---|---|
| Sản phẩm và SKU | Admin |
| Danh mục | Admin |
| Kho | Admin |
| Tổng quan tồn kho (theo kho, lọc sắp hết hàng) | Staff, Admin |
| Form nhập kho | Staff, Admin |
| Form điều chỉnh tồn kho | Admin |
| Sổ biến động (theo SKU/kho) | Staff, Admin |
| Danh sách đơn + thao tác Xuất kho / Hoàn tất | Staff, Admin |

Quy tắc giao diện: xử lý trạng thái đang tải, rỗng và lỗi; thông báo rõ ràng cho `409` (hết hàng / sai trạng thái)
và `429`. Thiết kế đơn giản (Tailwind); không tốn thời gian cho animation.

---

## 13. Chiến lược kiểm thử

### 13.1 Unit test
- Chuẩn hóa văn bản tiếng Việt.
- Tính tiền từng dòng và tổng tiền.
- Quy tắc chuyển trạng thái đơn hàng.
- Validator.
- Kiểm tra chữ ký webhook.

### 13.2 Integration test (Testcontainers + PostgreSQL)

Phần lớn test dùng **cơ chế xác thực dành cho test** (một header chọn người dùng và vai trò), để các test chạy song song
không tốn thời gian băm mật khẩu. Một vài test dùng đăng nhập bằng cookie thật để kiểm tra luồng này.

- Xác thực: `401` khi chưa đăng nhập (không phải chuyển hướng `302`), `403` khi sai vai trò ở endpoint admin.
- POST thiếu header `X-CSRF` → `403`.
- Khóa tài khoản sau 5 lần đăng nhập sai.
- **IDOR:** khách B xem đơn của khách A → `404`.
- Đặt hàng thành công; hết hàng → `409`; giá do client gửi bị bỏ qua.
- Gửi hai lần cùng `Idempotency-Key` → một đơn, cùng phản hồi; cùng key nhưng giỏ khác → `409 idempotency_key_reused`.
- Đơn chưa thanh toán thứ 4 → `409 too_many_pending_orders`.
- Nhập kho và điều chỉnh đều ghi sổ biến động; điều chỉnh giảm xuống dưới `Reserved` → `409`.
- Webhook: sai chữ ký → `401`; timestamp cũ hơn 5 phút → `401`; sai số tiền → payment `Failed`.
- Phản hồi đặt hàng có `ServerNow`; phản hồi lỗi có `traceId`.
- Xuất kho cập nhật đúng `OnHand`, `Reserved` và sổ biến động.
- Tìm kiếm: `dien thoai` ra `Điện thoại`.

### 13.3 Test đồng thời (bắt buộc, xem `Rule.md` §9)

| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| **C1** | 200 lượt đặt hàng song song (khách khác nhau) cho SKU chỉ còn 1 sản phẩm | đúng 1 thành công, 199 × `409`, `Reserved == 1`, `OnHand - Reserved >= 0` |
| **C2** | 50 lượt đặt hàng song song cùng một `Idempotency-Key` | đúng 1 đơn |
| **C3** | Cùng một webhook thành công gửi song song 10 lần | 1 payment được áp dụng, đơn chuyển `Paid` một lần |
| **C4** | Job hết hạn và webhook thành công tranh nhau trên cùng một đơn | chỉ một trong hai: `Paid` hoặc `Expired`; tồn kho nhất quán; thanh toán trễ → `RefundRequired` |
| **C5** | Ngẫu nhiên nhiều thao tác: nhập kho, điều chỉnh, đặt hàng, hủy, xuất kho | với mọi dòng tồn kho: `SUM(biến động) == OnHand`, `0 <= Reserved <= OnHand` |

C1 dùng 200 request để chạy được trên CI; phiên bản 1.000 người dùng chạy bằng k6 (`L2-flash-buy.js`).

### 13.4 Dữ liệu mẫu (Bogus, seed cố định)
- 4 danh mục cấp 1: Điện thoại, Laptop, Linh kiện máy tính, Phụ kiện (+ vài danh mục con).
- Khoảng 40 sản phẩm, 120 SKU, thương hiệu hư cấu, tên sản phẩm tiếng Việt có dấu.
- 3 kho: `HN01` (Hà Nội), `HCM01` (TP.HCM), `DN01` (Đà Nẵng).
- **Tồn kho ban đầu được tạo qua service nhập kho**, không bao giờ insert thẳng vào `stock_items`,
  để bất biến của sổ biến động (`SUM(biến động) == OnHand`) đúng ngay từ đầu.
- Seed có tính idempotent (chạy hai lần không thay đổi gì).
- Chỉ ở `LoadTest`: 1.000 tài khoản khách `load-0001@test.local` … `load-1000@test.local`.
- Tài khoản demo (chỉ Development/Demo): `customer1@demo.local`, `customer2@demo.local`,
  `staff@demo.local`, `admin@demo.local`. Mật khẩu demo được ghi trong README và đặt qua cấu hình.
- Một SKU chỉ còn đúng 1 sản phẩm để demo flash sale.

---

## 14. Lộ trình và danh sách task

Ký hiệu: ⭐ = task cốt lõi. Bạn nên thử tự viết trước, hoặc ít nhất phải hiểu hoàn toàn (xem `CLAUDE.md` §7).
Mỗi task đi theo quy trình đầy đủ ở `Rule.md` §1.

**Theo dõi bằng GitHub Issues:**
- Danh sách này là **kế hoạch**. Mỗi task có một **GitHub issue** (spec của task) khi bắt đầu làm, do `/to-spec` tạo.
  Không tạo issue trước, nên kế hoạch và issue không bao giờ bị lệch nhau.
- Khi issue được tạo, thêm link ngay sau mã task, ví dụ `**W3-03** (#14) Đặt hàng ...`.
- Mỗi issue thuộc milestone của tuần tương ứng (`Week 1` … `Week 5`, `Bonus`).
- Task xong khi PR (`Closes #14`) được merge. Lúc đó đánh dấu `[x]` ở đây và trong `Workflow.md`.

**Giai đoạn hiện tại:** Tuần 1 — chưa bắt đầu

### Tuần 1 — Nền tảng và Catalog

- [ ] **W1-01** Dựng repository: solution, thư mục theo `Rule.md` §2, `.editorconfig`, `Directory.Build.props`
  (nullable, coi cảnh báo là lỗi), `docker-compose.yml` (PostgreSQL), `.gitignore`, khung README.
  *Xong khi:* `docker compose up -d` + `dotnet run` khởi động được API.
- [ ] **W1-02** CI bằng GitHub Actions: build + test backend, lint + build frontend, kiểm tra lỗ hổng thư viện; Dependabot.
  *Xong khi:* PR hiện dấu tích xanh.
- [ ] **W1-03** Nền tảng dữ liệu: `AppDbContext`, đặt tên snake_case, quy ước UTC/decimal, `TimeProvider`,
  cấu hình connection pool, kiểm tra options khi khởi động, migration đầu tiên, `/health/live` và `/health/ready`.
- [ ] **W1-04** Xử lý lỗi và tài liệu API: ProblemDetails có `code` + `traceId`, nối FluentValidation,
  kiểu phản hồi danh sách chuẩn (§9), OpenAPI + Scalar.
- [ ] **W1-05** Identity: đăng ký, đăng nhập, đăng xuất, me; cấu hình cookie (§11.5); trả `401`/`403` thay vì chuyển hướng;
  kiểm tra header CSRF; khóa tài khoản; khóa Data Protection trong DB; vai trò + seed staff/admin; policy phân quyền;
  cơ chế xác thực dành cho integration test.
  *Xong khi:* có test chứng minh các trường hợp `401`, `403`, thiếu CSRF và khóa tài khoản.
- [ ] **W1-06** Quản trị Catalog: danh mục, sản phẩm, SKU (DTO, validator, sinh slug, cập nhật `MinPrice`).
- [ ] **W1-07** Catalog công khai: danh sách sản phẩm (lọc, phân trang) và chi tiết sản phẩm.
- [ ] **W1-08** Dữ liệu mẫu bằng Bogus (§13.4): danh mục, sản phẩm, SKU, người dùng. **Chưa có tồn kho** (thêm ở W2-03).
- [ ] **W1-09** Dựng frontend: hai app Vite, routing, Tailwind, TanStack Query, sinh kiểu API,
  trang đăng nhập, danh sách/chi tiết sản phẩm (storefront), quản lý sản phẩm (admin).

### Tuần 2 — Kho

- [ ] **W2-01** Kho (admin).
- [ ] **W2-02** Bảng tồn kho với ràng buộc CHECK và unique index.
- [ ] ⭐ **W2-03** Nhập kho, ghi sổ biến động trong cùng transaction; mở rộng seed để tạo tồn kho ban đầu qua service này.
- [ ] **W2-04** Điều chỉnh tồn kho (Admin, bắt buộc lý do, không giảm xuống dưới `Reserved`).
- [ ] **W2-05** Tổng quan tồn kho và truy vấn sổ biến động (phân trang keyset).
- [ ] **W2-06** Endpoint trạng thái hàng (`InStock` / `LowStock` / `OutOfStock`) và hiển thị ở trang chi tiết sản phẩm.
- [ ] **W2-07** Giao diện admin: kho, tổng quan tồn kho, form nhập kho, form điều chỉnh, trang sổ biến động.

### Tuần 3 — Giỏ hàng và đặt hàng

- [ ] **W3-01** API giỏ hàng.
- [ ] ⭐ **W3-02** Service giữ hàng của Inventory: `TryReserve`, `Release`, `Commit`, `Ship` (cập nhật nguyên tử, §7.2).
- [ ] ⭐ **W3-03** Đặt hàng (§8.1): chọn kho, sao chép giá, sequence mã đơn,
  tạo đơn + dòng đơn + reservation + dọn giỏ trong một transaction, idempotency key + request hash,
  giới hạn đơn chưa thanh toán, thử lại khi gặp deadlock/lỗi serialization.
- [ ] **W3-04** Danh sách/chi tiết đơn của tôi, có kiểm tra quyền sở hữu (test IDOR).
- [ ] **W3-05** Khách hủy đơn.
- [ ] ⭐ **W3-06** Test đồng thời C1 và C2.
- [ ] **W3-07** Storefront: trang giỏ hàng, thanh toán, đơn của tôi, chi tiết đơn.

### Tuần 4 — Thanh toán, hết hạn, tìm kiếm, hiệu năng

- [ ] **W4-01** Bắt đầu thanh toán + cổng giả lập (chỉ Dev/Demo, chỉ giao tiếp qua HTTP, gửi nền có thử lại,
  kiểm tra quyền sở hữu) + trang thanh toán giả lập có polling trạng thái.
- [ ] ⭐ **W4-02** Webhook thanh toán: chữ ký kèm timestamp, loại trùng, kiểm tra số tiền, đánh dấu đã thanh toán; test C3.
- [ ] ⭐ **W4-03** Job nền hết hạn giữ hàng; thanh toán trễ → `RefundRequired` → `Refunded`; test C4.
- [ ] **W4-04** Xuất kho và hoàn tất đơn (staff); test C5.
- [ ] **W4-05** Tìm kiếm tiếng Việt: chuẩn hóa, trigram index, bộ lọc.
- [ ] **W4-06** Cache danh mục sản phẩm + các policy rate limiting.
- [ ] **W4-07** Môi trường `LoadTest` + 1.000 tài khoản test tải; chạy script k6 L1 và L2 từ máy khác;
  đo trước/sau; so với mục tiêu ở §11.1; ghi kết quả vào README.

### Tuần 5 — Hoàn thiện và ra mắt

- [ ] **W5-01** Hoàn thiện giao diện: trạng thái đang tải/rỗng/lỗi, thông báo cho `409` và `429`.
- [ ] **W5-02** Rà soát khả năng quan sát (§11.4): log có cấu trúc, che dữ liệu, trace id, sự kiện nghiệp vụ,
  giám sát uptime. OpenTelemetry chỉ làm nếu còn thời gian.
- [ ] **W5-03** Dockerfile và compose production, Caddy (HTTPS, security headers), forwarded headers,
  image trên GitHub Container Registry, workflow deploy thủ công có migration bundle, backup hằng ngày
  **và một lần thử khôi phục**, xoay vòng log, seed môi trường Demo (§11.5, §11.6).
- [ ] **W5-04** Hoàn thiện README (§16), kiểm tra đủ các ADR ở §17, ảnh chụp hoặc GIF demo ngắn.
- [ ] **W5-05** Tập dượt kịch bản demo (§15); rà soát toàn bộ code theo `Rule.md` lần cuối.

### Tuần 6 — Điểm cộng B1: trợ lý AI (tùy chọn, chỉ khi xong Tuần 1–5)

- [ ] **B1-01** Các tool chỉ đọc: tìm sản phẩm, xem trạng thái hàng, xem trạng thái đơn của tôi.
- [ ] **B1-02** `/api/assistant/chat` dùng Microsoft.Extensions.AI, có rate limit, có feature flag.
- [ ] **B1-03** Khung chat trên storefront, hiển thị thẻ sản phẩm (link do backend tạo).
- [ ] **B1-04** Bộ đánh giá: 30 câu hỏi kèm đáp án mong đợi, chạy được như một test.

Quy tắc cho AI: tool chỉ được đọc; id người dùng chỉ lấy từ ngữ cảnh đăng nhập; link, giá và trạng thái hàng
lấy từ dữ liệu backend, không bao giờ do mô hình tự tạo; coi mọi văn bản là không đáng tin (`Rule.md` §11).

**Nếu hết thời gian:** dừng sau Tuần 4, rồi làm W5-03 và W5-04 (deploy + README). Như vậy vẫn là một dự án hoàn chỉnh.

---

## 15. Kịch bản demo (khoảng 3 phút)

1. Mở trang tồn kho admin: SKU `NOVAX-128-BLK` còn 1 sản phẩm ở `HCM01`.
2. Chạy `k6 run tests/load/L2-flash-buy.js` (1.000 người mua) → 1 thành công, 999 báo hết hàng; tồn kho không âm.
3. Đặt một đơn và không thanh toán → sau thời hạn (đặt 1 phút ở môi trường Demo) đơn chuyển `Expired`, hàng bán được trở lại.
4. Ở trang thanh toán giả lập, bấm "Gửi webhook trùng" → đơn chỉ được thanh toán một lần, chỉ có một payment.
5. Staff xuất kho đơn hàng → mở sổ biến động: mọi con số đều được giải thích bằng một dòng biến động.
6. Đăng nhập bằng khách khác và mở URL đơn của khách đầu tiên → `404`.
7. Tìm `dien thoai` → ra kết quả tiếng Việt có dấu.
8. (Điểm cộng) Hỏi trợ lý "Nova X 128GB màu đen còn hàng không?" → câu trả lời kèm thẻ sản phẩm và link.

---

## 16. Checklist README

- [ ] Mô tả một đoạn + lời cam kết cốt lõi
- [ ] Link demo + tài khoản demo cho từng vai trò
- [ ] Sơ đồ kiến trúc và sơ đồ trạng thái đơn/tồn kho
- [ ] "Bài toán khó và cách em giải quyết": P1–P7, mỗi bài 3–4 dòng
- [ ] Kết quả test: test đồng thời + số liệu k6 kèm cấu hình máy
- [ ] Cách chạy cục bộ bằng một lệnh
- [ ] Tech stack
- [ ] Giới hạn đã biết và cách xử lý lỗi (§11.3, §11.7)
- [ ] Hướng phát triển khi quy mô lớn hơn (§18)

---

## 17. Tài liệu trong repository

| Cái gì | Ở đâu | Tạo bởi |
|---|---|---|
| Spec của mỗi task | GitHub issue (theo mẫu của skill `to-spec`) | `/grill-with-docs` → `/to-spec` |
| Thuật ngữ nghiệp vụ | `GLOSSARY.md` | `/grill-with-docs` (domain-modeling) |
| Quyết định thiết kế | `docs/adr/NNNN-title.md` (bối cảnh, quyết định, phương án khác, hệ quả) | `/grill-with-docs`, hoặc viết ngay khi ra quyết định |
| Cấu hình cho agent skills | `docs/agents/*.md` | `/setup-matt-pocock-skills` (một lần) |

Mọi spec issue phải ghi trong mục "Further Notes": `Task: W?-??` và các mục liên quan trong `Workflow.md`.

Các ADR cần viết (chậm nhất là khi xong task liên quan):
1. `0001-atomic-conditional-update-for-stock.md` — so với khóa bi quan (pessimistic locking) và concurrency token lạc quan
2. `0002-append-only-stock-ledger.md`
3. `0003-idempotency-key-for-checkout.md`
4. `0004-cookie-auth-instead-of-jwt-in-browser.md`
5. `0005-no-redis-or-message-queue-yet.md`
6. `0006-csrf-protection-for-cookie-auth.md`
7. `0007-single-instance-deployment-and-backups.md`
8. `0008-github-issues-and-agent-skills-workflow.md`

(Issue, ADR và glossary viết bằng tiếng Anh để AI đọc thống nhất; bạn có thể bình luận thêm bằng tiếng Việt nếu cần.)

---

## 18. Ý chính khi phỏng vấn (cách mở rộng — giải thích, không làm)

| Câu hỏi | Hướng trả lời |
|---|---|
| Lưu lượng tăng gấp 100 lần? | Chạy thêm instance API (stateless), chuyển cache sang Redis, read replica cho phần catalog, CDN cho frontend |
| Flash sale thật? | Bộ đếm Redis làm cổng chặn trước database, hàng đợi xử lý đơn tuần tự, cache cờ "hết hàng" |
| Cổng thanh toán bị lỗi? | Retry có backoff, circuit breaker, job đối soát hỏi lại cổng thanh toán các giao dịch bị sót |
| Gửi sự kiện tin cậy (email, thống kê)? | Bảng Outbox ghi trong cùng transaction + message broker (RabbitMQ) |
| Đơn cần hàng từ 2 kho? | Tách thành nhiều kiện theo kho, mỗi kiện có trạng thái riêng |
| Vì sao chọn modular monolith? | Dễ xây dựng, deploy và test; có thể tách module ra sau nếu thật sự cần |

---

## 19. Nhật ký thay đổi kế hoạch

| Ngày | Thay đổi | Người duyệt |
|---|---|---|
| 2026-11-21 | Kế hoạch ban đầu | người dùng |
| 2026-11-22 | Đổi tên sản phẩm thành Gearnix (repository `gearnix`); tiền tố mã đơn `GN-` | người dùng |
| 2026-11-21 | Dùng mattpocock/skills: spec là GitHub issue (tạo khi bắt đầu từng task), `GLOSSARY.md`, `docs/adr/`, test trước theo từng lát dọc, `code-review` độc lập, người dùng tự push và merge | người dùng |
| 2026-11-21 | Rà soát theo System Design: mục tiêu và quy mô, độ tin cậy, khả năng quan sát, bảo mật, deploy và vận hành (§11); index và sequence mã đơn (§6.6); request hash cho idempotency, giới hạn đơn chưa thanh toán, chống gửi lại webhook và cơ chế gửi lại, dọn giỏ trong transaction đặt hàng; seed tồn kho qua nhập kho; trạng thái hàng thành endpoint riêng | người dùng |
