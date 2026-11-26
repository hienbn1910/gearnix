# Gearnix — Kế hoạch dự án (bản tiếng Việt)

> **Phạm vi tài liệu.** Đây là bản dịch tiếng Việt của `Workflow.md`, dành cho chủ dự án đọc.
>
> - Bản tiếng Anh `Workflow.md` là bản chính thức. **Nếu hai bản khác nhau, bản tiếng Anh là chuẩn.**
> - Khi sửa kế hoạch: sửa `Workflow.md` trước, rồi cập nhật bản này cho khớp, và ghi vào §19.
> - Cách viết code được quy định trong `Rule.md`.
> - Số mục được `CLAUDE.md`, `Rule.md` và các GitHub issue tham chiếu tới. Không đánh số lại.
> - Các chỗ đánh dấu **[Cần xác nhận]** là câu hỏi còn mở; danh sách đầy đủ ở §20.

---

## 1. Tóm tắt dự án

**Gearnix** là hệ thống thương mại điện tử bán đồ điện tử (điện thoại, laptop, linh kiện máy tính, phụ kiện),
xây dựng xoay quanh **quản lý tồn kho nhiều kho**. Website bán hàng được giữ tối giản có chủ đích: nhiệm vụ của nó
là tạo ra đơn hàng thật để vận hành phần logic kho.

| | |
|---|---|
| **Mục đích** | Dự án portfolio để ứng tuyển **thực tập sinh .NET backend** |
| **Thứ tự ưu tiên** | Làm xong > làm đúng > deploy được > giải thích được > số lượng tính năng |
| **Cam kết cốt lõi** | *"Không bao giờ bán vượt tồn kho, và mọi thay đổi tồn kho đều truy vết được."* |
| **Tên gọi** | "Gearnix" (repository `gearnix`) chỉ dùng cho dự án này. Mọi thương hiệu và sản phẩm trong dữ liệu mẫu đều hư cấu; không sao chép tên, logo, hình ảnh hay giao diện của cửa hàng thật. |

---

## 2. Mục tiêu và những gì không làm

### 2.1 Bài toán cốt lõi (bắt buộc)

| ID | Bài toán | Thế nào là xong |
|---|---|---|
| **P1** | Nhiều người tranh mua lượng hàng ít | 1.000 lượt đặt hàng đồng thời cho 1 sản phẩm → đúng 1 đơn thành công; tồn kho không bao giờ âm |
| **P2** | Giữ hàng có thời hạn | Hàng do đơn chưa thanh toán giữ sẽ tự động được trả lại sau 15 phút |
| **P3** | Idempotency | Bấm "Đặt hàng" hai lần chỉ tạo 1 đơn; webhook thanh toán gửi trùng chỉ được áp dụng 1 lần |
| **P4** | Sổ biến động kho | Mọi thay đổi `OnHand` đều có một dòng trong sổ chỉ-được-thêm; tổng sổ luôn bằng `OnHand` |
| **P5** | Tính giá phía server và phân quyền | Giá được tính ở server và chụp lại vào đơn; không người dùng nào xem được đơn của người khác (IDOR) |
| **P6** | Tìm kiếm tiếng Việt | Gõ `dien thoai` vẫn ra `Điện thoại`; lọc theo danh mục, thương hiệu, khoảng giá |
| **P7** | Tải đột biến (mức cơ bản) | Cache catalog và rate limiting, có số liệu trước/sau đo bằng k6 |

### 2.2 Điểm cộng (chỉ làm sau khi xong toàn bộ phần cốt lõi)

| ID | Tính năng |
|---|---|
| **B1** | Trợ lý AI trả lời "X còn hàng không?" từ dữ liệu kho thật và trả về link sản phẩm |

### 2.3 Những gì không làm

Các mục sau nằm ngoài phạm vi, trừ khi chủ dự án yêu cầu rõ ràng:

Redis, RabbitMQ, Outbox pattern, microservices, hàng đợi flash sale, voucher/mã giảm giá, combo/quà tặng,
quản lý serial/IMEI, tách một đơn ra nhiều kho, chuyển kho, đổi trả/bảo hành, bán đa kênh (sàn TMĐT, POS),
cổng thanh toán thật, đơn vị vận chuyển thật, đánh giá, wishlist, gợi ý sản phẩm, so sánh sản phẩm,
kiểm tra tương thích build PC, thu cũ đổi mới, trả góp, hóa đơn điện tử, gửi email, Kubernetes.

> Các mục này được loại ra có chủ đích. Mục tiêu là *giải thích được* cách thêm từng mục (§18), không phải xây dựng chúng.

---

## 3. Người dùng và vai trò

| Vai trò | Quyền |
|---|---|
| **Anonymous** (khách vãng lai) | Xem và tìm sản phẩm; xem trạng thái hàng (Còn hàng / Sắp hết / Hết hàng) |
| **Customer** | Quyền của Anonymous, cộng thêm: giỏ hàng, đặt hàng, thanh toán giả lập, xem và hủy đơn **của mình** |
| **WarehouseStaff** | Xem tồn kho và sổ biến động, tạo phiếu nhập kho, xem đơn hàng, xuất kho đơn đã thanh toán |
| **Admin** | Quyền của WarehouseStaff **trừ xuất kho và hoàn tất đơn**, cộng thêm: quản lý danh mục, sản phẩm, SKU, giá, kho và điều chỉnh tồn kho |

- Mỗi tài khoản có đúng một vai trò.
- Customer tự đăng ký. Tài khoản WarehouseStaff và Admin chỉ được tạo bằng dữ liệu mẫu (seed).
- **Tách nhiệm vụ (separation of duties):** chỉ WarehouseStaff xuất kho và hoàn tất đơn. Admin được điều chỉnh tồn kho;
  nếu cùng vai trò đó còn được xuất kho, một người có thể vừa đưa hàng ra vừa sửa số liệu sau đó. Tách hai quyền này
  ra thì mọi biến động `Shipment` đều do nhân viên kho tạo.

---

## 4. Công nghệ sử dụng

| Mảng | Công nghệ | Ghi chú |
|---|---|---|
| Runtime | .NET 10 (LTS), C# | |
| API | ASP.NET Core (controller) | Controller mỏng; xem `Rule.md` §2 |
| Truy cập dữ liệu | EF Core 10 + Npgsql | `ExecuteUpdateAsync` để cập nhật tồn kho nguyên tử |
| Công cụ EF Core | Microsoft.EntityFrameworkCore.Design + công cụ `dotnet-ef` | Migration và migration bundle |
| Đặt tên trong database | EFCore.NamingConventions | Tên snake_case (§6); package cộng đồng — kiểm tra đã hỗ trợ EF Core 10 khi cài |
| Database | PostgreSQL 17 trở lên | Extension `pg_trgm` cho tìm kiếm |
| Kiểm tra đầu vào | FluentValidation (+ FluentValidation.DependencyInjectionExtensions) | Bắt buộc kiểm tra ở backend |
| Xác thực | ASP.NET Core Identity, xác thực bằng cookie | Cookie HttpOnly, Secure, SameSite; không lưu JWT trên trình duyệt |
| Lưu Identity và khóa | Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | User/role và khóa Data Protection lưu trong PostgreSQL (§6.5) |
| Lỗi | ProblemDetails | Một định dạng lỗi chung cho mọi endpoint |
| Tài liệu API | Microsoft.AspNetCore.OpenApi + Scalar | |
| Cache | HybridCache (trong bộ nhớ, một instance) | Không dùng Redis (ngoài phạm vi) |
| Rate limiting | Rate limiter có sẵn của ASP.NET Core | |
| Job nền | `BackgroundService` + `PeriodicTimer` | Giải phóng hàng giữ quá hạn |
| Frontend | React + TypeScript + Vite | Hai app: `storefront` và `admin` |
| Thư viện frontend | React Router, TanStack Query, Tailwind CSS, Zod | Zod chỉ dùng cho form |
| Kiểu dữ liệu API | openapi-typescript | Sinh từ tài liệu OpenAPI của backend |
| Kiểm thử | xUnit, Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing | Integration test gọi API trong bộ nhớ qua HTTP (`WebApplicationFactory`), chạy trên PostgreSQL thật |
| Dữ liệu mẫu | Bogus | Được project API tham chiếu; bộ seed chỉ chạy ở `Development`, `Demo` và `LoadTest` (§13.4) |
| Load test | k6 | |
| Môi trường cục bộ | Docker Compose | |
| CI | GitHub Actions | Build, test và lint mỗi pull request vào `main` và mỗi lần push lên `main` |
| Hosting | VPS + Docker Compose + Caddy (HTTPS) | Chọn nhà cung cấp ở W5-03 (§11.6) |
| AI (điểm cộng) | Microsoft.Extensions.AI | Ollama khi chạy máy; mô hình hosted khi demo |
| Theo dõi task | GitHub Issues + milestone, `gh` CLI | Spec nằm trong issue (§17) |
| Quy trình với agent | Claude Code + mattpocock/skills | Quy trình ở `Rule.md` §1 |

Thư viện ngoài bảng này cần chủ dự án đồng ý (`Rule.md` §7).

---

## 5. Kiến trúc

### 5.1 Kiểu kiến trúc

Gearnix là một **modular monolith**: một project ASP.NET Core (`src/Api`), một database PostgreSQL và một `AppDbContext`,
code được chia thành các module. Cấu trúc thư mục quy định trong `Rule.md` §2.

```
 Trình duyệt (storefront)   Trình duyệt (admin)
          \                  /
           \   HTTPS /api   /
            v              v
        ┌───────────────────────────┐
        │      ASP.NET Core API      │
        │  Identity  Catalog         │
        │  Inventory Ordering Payment│
        │  Job nền (hết hạn giữ hàng)│
        └─────────────┬─────────────┘
                      │
                 PostgreSQL
```

Ở production, Caddy phục vụ cả hai app React và chuyển tiếp `/api` tới API trên **cùng một tên miền**, nên cookie
hoạt động mà không cần cấu hình cross-site. Khi dev, proxy của Vite dev server đóng vai trò tương tự.

### 5.2 Các module

| Module | Sở hữu | Service công khai cho module khác |
|---|---|---|
| **Identity** | Người dùng, vai trò, đăng nhập | `ICurrentUser` (id, vai trò) |
| **Catalog** | Danh mục, sản phẩm, SKU, giá, tìm kiếm | `ICatalogService.GetSkusForCheckoutAsync(ids)` |
| **Inventory** | Kho, tồn kho, sổ biến động, giữ hàng | `IInventoryService`: `TryReserveAsync`, `ReleaseAsync`, `CommitAsync`, `ShipAsync`, `GetStockStatusAsync` |
| **Ordering** | Giỏ hàng, đơn hàng, dòng đơn, máy trạng thái đơn | `IOrderingService`: `MarkPaidAsync`, `GetOrderForPaymentAsync` |
| **Payment** | Thanh toán, xử lý webhook, cổng thanh toán giả lập | — |

### 5.3 Quy tắc phụ thuộc

```
Ordering ──► Catalog     (giá, thông tin SKU)
Ordering ──► Inventory   (giữ / trả / xuất hàng)
Payment  ──► Ordering    (đánh dấu đơn đã thanh toán)
```

- **Không có phụ thuộc vòng.** Inventory không bao giờ phụ thuộc Ordering; nó chỉ lưu tham chiếu (`ReferenceType`, `ReferenceId`).
- Các module chỉ gọi nhau qua các service interface công khai ở trên (`Rule.md` §2).
- **Transaction có thể trải qua nhiều module.** Vì mọi module dùng chung một `AppDbContext`, một service có thể mở
  transaction rồi gọi service của module khác bên trong — ví dụ Ordering tạo đơn trong khi Inventory giữ hàng, trong
  **cùng một** transaction. Service nào mở transaction thì service đó sở hữu transaction.
- **Không dùng navigation property của EF giữa các module.** Entity chỉ tham chiếu dữ liệu module khác bằng id
  (`OrderLine.SkuId`, không phải `OrderLine.Sku`), nhờ đó ranh giới module được giữ thật sự.
- **Cổng thanh toán giả lập hoạt động như một hệ thống bên ngoài.** Nó chỉ giao tiếp với module Payment qua HTTP
  (webhook), không bao giờ gọi trực tiếp service hay database.

---

## 6. Mô hình dữ liệu

Quy ước cho mọi bảng (các cột suy ra từ quy ước này không được lặp lại ở bảng bên dưới):

- **Khóa chính:** `Guid` sinh bằng `Guid.CreateVersion7()` (sắp xếp được theo thời gian, khó đoán).
- **Thời gian:** `timestamptz`, luôn là UTC.
- **Tiền:** `numeric(18,2)` ↔ `decimal` trong C#. Chỉ dùng VND.
- **Cột kiểm toán:** bảng nào cũng có `CreatedAt`; bảng có thể sửa thì có thêm `UpdatedAt`.
- **Đặt tên:** snake_case trong database (qua naming convention của EF Core), PascalCase trong C#.

### 6.1 Catalog

**categories**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| Name | text | Ví dụ "Điện thoại" |
| Slug | text | Duy nhất, ví dụ `dien-thoai` |
| ParentId | uuid? | FK → categories; tối đa 2 cấp |
| SortOrder | int | |

**products**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| CategoryId | uuid | FK |
| Name | text | Ví dụ "Điện thoại Nova X 5G" |
| NameNormalized | text | Chữ thường, bỏ dấu, `đ`→`d`; C# tính khi lưu; có GIN trigram index |
| Slug | text | Duy nhất |
| Brand | text | Thương hiệu hư cấu |
| Description | text | |
| Specs | jsonb | Thông số theo danh mục, ví dụ `{"cpu": "...", "ramGb": 16}` |
| MinPrice | numeric(18,2) | Giá thấp nhất trong các SKU đang hoạt động; cập nhật trong cùng transaction với mọi thay đổi SKU; dùng để lọc và sắp xếp theo giá |
| IsActive | bool | Sản phẩm không hoạt động bị ẩn khỏi website |

**skus** (biến thể bán được)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| ProductId | uuid | FK |
| SkuCode | text | Duy nhất, ví dụ `NOVAX-128-BLK` |
| VariantName | text | Ví dụ "128GB - Đen" |
| Price | numeric(18,2) | Giá hiện tại; phải > 0 |
| Attributes | jsonb | Ví dụ `{"storage": "128GB", "color": "Đen"}` |
| IsActive | bool | |

### 6.2 Inventory

**warehouses**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| Code | text | Duy nhất, ví dụ `HN01`, `HCM01`, `DN01` |
| Name | text | |
| City | text | Dùng để chọn kho giao hàng (§7.4) |
| IsActive | bool | |

**stock_items** — mỗi dòng ứng với một cặp (SKU, kho)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| SkuId | uuid | FK |
| WarehouseId | uuid | FK |
| OnHand | int | Số lượng thực tế trong kho |
| Reserved | int | Số lượng đang giữ cho đơn chưa xuất kho |

- Ràng buộc: `UNIQUE (SkuId, WarehouseId)`, `CHECK (OnHand >= 0)`, `CHECK (Reserved >= 0)`, `CHECK (Reserved <= OnHand)`.
- `Available = OnHand - Reserved` **chỉ được tính, không bao giờ lưu**.
- Các ràng buộc CHECK là lớp bảo vệ cuối cùng. Luồng xử lý bình thường không được dựa vào chúng.

**stock_movements** — sổ biến động kho (chỉ được thêm: không `UPDATE`, không `DELETE`)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| SkuId, WarehouseId | uuid | |
| Type | text | `Receipt` (nhập), `Shipment` (xuất), `Adjustment` (điều chỉnh) |
| QuantityDelta | int | Thay đổi của `OnHand`; dương hoặc âm, không bao giờ bằng 0 |
| OnHandAfter | int | `OnHand` ngay sau biến động này |
| ReferenceType | text? | Ví dụ `Order`, `GoodsReceipt` |
| ReferenceId | uuid? | |
| Reason | text? | Bắt buộc với `Adjustment` |
| CreatedBy | uuid | Id người thực hiện |
| CreatedAt | timestamptz | |

- Index: `(SkuId, WarehouseId, CreatedAt, Id)`.
- **Bất biến:** với mỗi dòng tồn kho, `SUM(QuantityDelta) == OnHand`.
- Giữ hàng **không** tạo dòng biến động: nó thay đổi `Reserved`, không thay đổi `OnHand`.

**stock_reservations**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderId | uuid | Chỉ là tham chiếu; không có FK sang bảng của Ordering |
| SkuId, WarehouseId | uuid | |
| Quantity | int | Phải > 0 |
| Status | text | `Active`, `Committed`, `Released`, `Shipped` |

Hạn thanh toán lưu ở đơn hàng (`orders.ExpiresAt`), không lưu ở reservation.

### 6.3 Ordering

- **carts:** `Id`, `CustomerId` (duy nhất), `UpdatedAt`
- **cart_items:** `CartId`, `SkuId`, `Quantity` (1–5); khóa chính `(CartId, SkuId)`

**orders**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderNumber | text | Duy nhất, dễ đọc, ví dụ `GN-20261121-000123` |
| CustomerId | uuid | Chủ đơn |
| Status | text | Xem §7.3 |
| WarehouseId | uuid | Kho giao hàng |
| Subtotal, Total | numeric(18,2) | `Total = Subtotal` (không có phí ship hay giảm giá trong phạm vi) |
| IdempotencyKey | text | `UNIQUE (CustomerId, IdempotencyKey)` |
| RequestHash | text | SHA-256 của request đặt hàng đã chuẩn hóa (§7.7) |
| ShippingName, ShippingPhone, ShippingAddress, ShippingCity | text | |
| ExpiresAt | timestamptz | Hạn thanh toán |
| PaidAt, ShippedAt, CompletedAt, CancelledAt, ExpiredAt | timestamptz? | Ghi khi đơn chuyển sang trạng thái tương ứng |

`Status` là nguồn sự thật duy nhất cho biết đơn kết thúc vì sao: `Cancelled` (khách hủy) hoặc `Expired`
(quá hạn thanh toán). Không có cột lý do hủy riêng, nên hai thông tin không bao giờ mâu thuẫn nhau.

**order_lines**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderId | uuid | FK |
| SkuId | uuid | |
| ProductNameSnapshot, VariantNameSnapshot, SkuCodeSnapshot | text | Sao chép lúc đặt hàng |
| UnitPrice | numeric(18,2) | Sao chép lúc đặt hàng |
| Quantity | int | |
| LineTotal | numeric(18,2) | `UnitPrice * Quantity` |

### 6.4 Payment

**payments**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| Id | uuid | Khóa chính |
| OrderId | uuid | |
| Provider | text | `Mock` |
| ProviderTransactionId | text? | **Duy nhất**; ghi từ webhook đầu tiên của payment này; dùng để loại webhook trùng |
| Amount | numeric(18,2) | |
| Status | text | `Pending`, `Succeeded`, `Failed`, `RefundRequired`, `Refunded` |
| CreatedAt, UpdatedAt | timestamptz | |

### 6.5 Identity

- Các bảng chuẩn của ASP.NET Core Identity (users, roles, user roles), user có thêm các trường `FullName`, `Phone`, `City`.
- `data_protection_keys`: khóa Data Protection của ASP.NET Core, dùng để mã hóa cookie đăng nhập. Khóa được lưu trong
  database để mỗi lần deploy lại container không làm mọi người dùng bị đăng xuất (§11.5).

### 6.6 Index, sequence và giá trị tự sinh

| Bảng | Index / ràng buộc | Dùng cho |
|---|---|---|
| products | unique `Slug`; `(CategoryId)`; `(MinPrice)`; GIN trigram trên `NameNormalized` | Danh sách catalog, tìm kiếm |
| skus | unique `SkuCode`; `(ProductId)` | Chi tiết sản phẩm, đặt hàng |
| stock_items | unique `(SkuId, WarehouseId)`; `(WarehouseId)` | Giữ hàng, tổng quan tồn kho |
| stock_movements | `(SkuId, WarehouseId, CreatedAt, Id)` | Trang sổ biến động (phân trang keyset) |
| stock_reservations | `(OrderId)` | Trả / cam kết / xuất hàng |
| orders | unique `OrderNumber`; unique `(CustomerId, IdempotencyKey)`; `(CustomerId, CreatedAt DESC)`; `(Status, CreatedAt)`; partial `(ExpiresAt) WHERE Status = 'PendingPayment'` | Đơn của tôi, danh sách admin, **job hết hạn** |
| order_lines | `(OrderId)` | Chi tiết đơn |
| payments | unique `ProviderTransactionId`; `(OrderId)` | Loại webhook trùng |

- **Mã đơn hàng:** sinh từ sequence `order_number_seq` của PostgreSQL, an toàn khi có nhiều request đồng thời.
  Định dạng `GN-{yyyyMMdd}-{seq:D6}`, ngày theo giờ Việt Nam (`Asia/Ho_Chi_Minh`).
  Sequence không bao giờ reset; được phép nhảy số.
- **Đồng hồ:** `TimeProvider` của ứng dụng là nguồn thời gian duy nhất — không dùng `now()` của database — để test điều khiển được thời gian.

---

## 7. Quy tắc nghiệp vụ

### 7.1 Số lượng tồn kho

- `OnHand` — số lượng thực tế trong kho.
- `Reserved` — số lượng đã hứa cho các đơn chưa xuất kho.
- `Available = OnHand - Reserved` — số lượng còn bán được.

Khách hàng không bao giờ thấy số lượng chính xác. Họ thấy **trạng thái hàng**:

| Trạng thái | Tổng số còn bán được ở các kho đang hoạt động |
|---|---|
| `InStock` (Còn hàng) | > 5 |
| `LowStock` (Sắp hết) | 1–5 |
| `OutOfStock` (Hết hàng) | 0 |

WarehouseStaff và Admin thấy số lượng chính xác theo từng kho.

Trạng thái hàng là tổng của mọi kho, nhưng mỗi đơn chỉ lấy hàng từ **một** kho (§7.4).
Vì vậy đặt hàng vẫn có thể trả `409` dù trang sản phẩm đang hiện "Còn hàng"; giao diện phải xử lý trường hợp này.

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
- Đơn có nhiều dòng thì giữ hàng cho mọi dòng **trong một transaction**. Một dòng thất bại thì cả transaction bị hoàn tác.
- Các dòng được giữ **theo thứ tự `SkuId`** để khóa dòng luôn được lấy theo cùng một thứ tự, nhờ đó tránh deadlock.

### 7.3 Vòng đời đơn hàng

```
                ┌──────────► Cancelled (khách hủy)
                │
PendingPayment ─┼──────────► Expired   (quá hạn thanh toán)
                │
                └─► Paid ──► Shipped ──► Completed
```

| Chuyển trạng thái | Ai kích hoạt | Ảnh hưởng tồn kho |
|---|---|---|
| (mới) → `PendingPayment` | Khách đặt hàng | `Reserved += q`; reservation `Active` |
| `PendingPayment` → `Paid` | Webhook thanh toán | Reservation `Committed` (vẫn giữ, không còn hết hạn) |
| `PendingPayment` → `Cancelled` | Khách | `Reserved -= q`; reservation `Released` |
| `PendingPayment` → `Expired` | Job hết hạn | `Reserved -= q`; reservation `Released` |
| `Paid` → `Shipped` | WarehouseStaff | `OnHand -= q`, `Reserved -= q`; biến động `Shipment`; reservation `Shipped` |
| `Shipped` → `Completed` | WarehouseStaff (giả lập giao thành công) | Không |

- Mọi chuyển trạng thái khác đều bị từ chối với `409 Conflict`.
- Đổi trạng thái là một câu cập nhật có điều kiện trên trạng thái hiện tại (`... WHERE Id = @id AND Status = 'PendingPayment'`),
  nên hai hành động tranh nhau — ví dụ thanh toán và hết hạn — không bao giờ cùng thành công.
- Hủy đơn đã `Paid` nằm ngoài phạm vi.

### 7.4 Chọn kho giao hàng (không tách đơn)

1. Lập danh sách kho ứng viên: các kho đang hoạt động, kho có `City == ShippingCity` đứng trước, sau đó sắp theo `Code`.
2. Với từng kho ứng viên, thử giữ hàng cho **tất cả** các dòng đơn trong một transaction.
3. Kho đầu tiên thành công sẽ giao toàn bộ đơn.
4. Nếu không kho nào đáp ứng được cả đơn, trả `409 Conflict` kèm danh sách SKU bị thiếu.

### 7.5 Giá

- Khi đặt hàng, server đọc giá SKU hiện tại. Mọi giá do client gửi lên đều bị bỏ qua.
- Giá, tên sản phẩm, tên biến thể và mã SKU được sao chép vào `order_lines` (**snapshot**).
  Thay đổi catalog sau đó không ảnh hưởng đơn đã có.
- `LineTotal = UnitPrice * Quantity`; `Total = Σ LineTotal`. VND thực tế không có phần lẻ, nhưng vẫn dùng `decimal`.
- Không đặt được sản phẩm và SKU đang không hoạt động.

### 7.6 Giỏ hàng

- Mỗi khách một giỏ; mỗi dòng 1–5 sản phẩm.
- Thêm vào giỏ **không** giữ hàng.
- Giỏ hiển thị giá và trạng thái hàng hiện tại; lúc đặt hàng mọi thứ được kiểm tra lại.
- Sau khi đặt hàng thành công, các sản phẩm đã đặt được xóa khỏi giỏ **trong cùng transaction**.
- **Mỗi khách có tối đa 3 đơn `PendingPayment`.** Lần đặt thứ tư trả `409 too_many_pending_orders`.
  Nếu không giới hạn, một người có thể khóa hết hàng bằng các đơn không thanh toán.

### 7.7 Idempotency khi đặt hàng

- `POST /api/orders` bắt buộc có header `Idempotency-Key`: UUID do client sinh, mỗi lần thử đặt hàng một key.
- Database bảo đảm `UNIQUE (CustomerId, IdempotencyKey)`.
- `RequestHash` là SHA-256 của request đã chuẩn hóa (các dòng giỏ sắp theo `SkuId`, cộng thông tin giao hàng).

| Tình huống | Kết quả |
|---|---|
| Cùng key, cùng hash | Trả về **đơn đã có** với cùng phản hồi (`200`); không tạo đơn mới |
| Cùng key, khác hash | `409 idempotency_key_reused` |
| Lần thử trước thất bại (ví dụ hết hàng) | Không có gì được lưu, nên thử lại với cùng key sẽ chạy lại |
| Hai request đồng thời cùng key | Một request insert thành công; request còn lại gặp lỗi vi phạm ràng buộc duy nhất |

Trong PostgreSQL, lỗi vi phạm ràng buộc duy nhất **làm hỏng transaction hiện tại**. Phải rollback, rồi đọc đơn đã có bằng một truy vấn mới.

### 7.8 Hết hạn giữ hàng

- Hạn thanh toán: `ExpiresAt = CreatedAt + 15 phút`, cấu hình qua `Ordering:PaymentTimeoutMinutes`.
- Một `BackgroundService` chạy mỗi 60 giây, xử lý các đơn `PendingPayment` quá hạn theo lô 100 đơn.
- Với mỗi đơn, job cập nhật có điều kiện sang `Expired`; **chỉ khi cập nhật thành công** thì mới trả lại hàng đã giữ.
- Cập nhật có điều kiện theo từng đơn giúp job vẫn an toàn nếu sau này chạy trên nhiều instance.
- Job dừng gọn gàng khi ứng dụng tắt (`CancellationToken`). Mỗi đơn được xử lý trong một transaction riêng,
  nên lô bị ngắt giữa chừng không để lại trạng thái dở dang.
- Phản hồi đơn hàng có cả `ExpiresAt` và `ServerNow`. Đồng hồ đếm ngược ở frontend dùng hiệu của hai giá trị này,
  nên đồng hồ sai trên máy khách không ảnh hưởng.

### 7.9 Thanh toán (giả lập)

**Luồng**

1. Khách bắt đầu thanh toán → tạo payment `Pending` và gửi id của nó sang cổng giả lập làm mã tham chiếu của merchant
   → chuyển khách tới trang thanh toán giả lập.
2. Cổng giả lập gửi webhook tới `POST /api/payments/webhook`:
   - Body: `paymentId` (mã tham chiếu của merchant, được gửi trả lại), `providerTransactionId`, `orderId`, `amount`,
     `status` (`Succeeded` hoặc `Failed`).
   - Header `X-Timestamp`: thời điểm gửi webhook, tính bằng giây Unix.
   - Header `X-Signature`: HMAC-SHA256 của `"{timestamp}.{rawBody}"`, dùng khóa bí mật chung lấy từ cấu hình/secret.
3. Storefront biết kết quả bằng cách **polling** (hỏi lại liên tục) `GET /api/orders/{id}` mỗi 2 giây, tối đa 60 giây
   (WebSocket/SignalR nằm ngoài phạm vi).

**Cách cổng giả lập gửi webhook.** Webhook được gửi từ một **worker chạy nền** dùng `Channel` trong bộ nhớ, không bao giờ
gửi ngay trong luồng xử lý request. Phản hồi không phải 2xx được gửi lại tối đa 3 lần (sau 1 giây, 5 giây và 25 giây),
giống cổng thanh toán thật — đây chính là lý do webhook phải idempotent.

**Xử lý webhook**

1. Kiểm tra chữ ký bằng phép so sánh thời gian cố định (constant-time) và từ chối timestamp cũ hơn 5 phút
   (chống gửi lại webhook cũ) → `401`, ghi log cảnh báo.
2. Loại trùng theo `ProviderTransactionId` (duy nhất). Nếu đã xử lý rồi → `200`, không làm gì thêm.
3. Tìm payment theo `paymentId`. Payment phải tồn tại và thuộc về `orderId`; nếu không → `400`, ghi log cảnh báo.
   Ghi `ProviderTransactionId` vào payment đó.
4. Số tiền phải bằng tổng đơn. Nếu không → payment `Failed`, ghi log cảnh báo.
5. `Succeeded` và đơn vẫn `PendingPayment` → đơn `Paid`, reservation `Committed`.
6. `Succeeded` nhưng đơn đã `Expired` hoặc `Cancelled` → payment `RefundRequired`, rồi hoàn tiền giả lập → `Refunded`.
7. `Failed` → payment `Failed`. Đơn vẫn `PendingPayment` đến khi hết hạn, và khách có thể thử lại.

Khách thử lại sau khi thanh toán thất bại sẽ tạo một dòng payment mới, nên một đơn có thể có nhiều payment.
`paymentId` được gửi trả lại giúp xác định đúng dòng mà không phải đoán (ví dụ cách "lấy payment đang chờ mới nhất"
sẽ sai khi hai lần thử chồng lên nhau). Cổng thanh toán thật cũng làm như vậy với mã tham chiếu của merchant.

**Endpoint cổng giả lập.** `/api/mock-gateway/...` **chỉ** tồn tại ở môi trường `Development` và `Demo`.
Nó giả lập được: thành công, thất bại, webhook trùng (gửi hai lần) và webhook gửi trễ.
Người gọi phải là chủ của payment: khách không thể thanh toán đơn của người khác, kể cả ở Demo.

### 7.10 Nghiệp vụ kho

- **Nhập kho** (WarehouseStaff, Admin): thêm số lượng cho một hoặc nhiều SKU vào một kho.
  Với mỗi dòng: `OnHand += q` và một biến động `Receipt`, tất cả trong một transaction.
- **Điều chỉnh** (chỉ Admin): sửa tăng hoặc giảm, bắt buộc có lý do (ví dụ kiểm kê, hàng hỏng).
  Điều chỉnh giảm không được làm `OnHand < Reserved` → `409 adjustment_below_reserved`.
- **Xuất kho** (WarehouseStaff): chỉ áp dụng cho đơn `Paid`; xem §7.3.

### 7.11 Phân quyền truy cập

- Endpoint của khách luôn lọc theo id người dùng lấy từ cookie đăng nhập.
- Truy cập đơn của khách khác trả `404`, không phải `403`, để phản hồi không để lộ việc đơn đó tồn tại.
- Endpoint của WarehouseStaff và Admin nằm dưới `/api/admin/...` và được bảo vệ bằng policy theo vai trò.

---

## 8. Các luồng chính

### 8.1 Đặt hàng — `POST /api/orders`

**Trước transaction.** Giữ transaction thật ngắn: các dòng tồn kho đã giữ bị khóa cho tới khi commit.

1. Bắt buộc Customer đã đăng nhập, có header CSRF (§11.5) và `Idempotency-Key`.
2. Kiểm tra đầu vào (thông tin giao hàng, giỏ có ít nhất một dòng) và tính `RequestHash`.
3. Nếu khách đã có đơn với key này: cùng hash → trả đơn đó; khác hash → `409 idempotency_key_reused`.
4. Nếu khách đang có 3 đơn `PendingPayment` → `409 too_many_pending_orders`.
5. Lấy các dòng trong giỏ và SKU từ Catalog (chỉ SKU đang hoạt động, giá hiện tại). Tính tiền từng dòng và tổng đơn.
6. Lập danh sách kho ứng viên (§7.4).

**Mỗi kho ứng viên một transaction.**

7. Giữ hàng cho mọi dòng theo thứ tự `SkuId` (§7.2). Một dòng thất bại → rollback và thử kho tiếp theo.
8. Lấy mã đơn tiếp theo; thêm đơn (`PendingPayment`, `ExpiresAt`), các dòng đơn (snapshot) và các reservation (`Active`).
9. Xóa các sản phẩm đã đặt khỏi giỏ.
10. Commit. Nếu gặp deadlock hoặc lỗi serialization, chạy lại toàn bộ transaction (§11.3).

**Sau transaction.**

11. Trả `201` kèm id đơn, mã đơn, tổng tiền, `ExpiresAt` và `ServerNow`.
12. Lỗi: không kho nào đáp ứng được → `409 out_of_stock` kèm các SKU bị thiếu; đầu vào sai → `400`; chưa đăng nhập → `401`.

### 8.2 Webhook thanh toán — `POST /api/payments/webhook`

Endpoint không cần đăng nhập, được bảo vệ bằng chữ ký; các bước ở §7.9. Phải phản hồi nhanh và
không gọi dịch vụ bên ngoài trong transaction.

### 8.3 Job hết hạn

Mỗi 60 giây: lấy tối đa 100 id đơn có `Status = PendingPayment AND ExpiresAt < now`.
Với mỗi đơn, trong một transaction riêng: cập nhật trạng thái có điều kiện → nếu đúng một dòng thay đổi thì trả hàng đã giữ
(`Reserved -= q`, reservation `Released`). Ghi log số đơn hết hạn ở mức `Information`.

### 8.4 Nhập kho — `POST /api/admin/stock/receipts`

Kiểm tra các dòng → transaction { với mỗi dòng: tạo hoặc cập nhật dòng tồn kho, `OnHand += q`, thêm biến động `Receipt` kèm `OnHandAfter` } → `201`.

### 8.5 Xuất kho — `POST /api/admin/orders/{id}/ship`

Transaction { cập nhật trạng thái có điều kiện `Paid → Shipped` → với mỗi reservation: `OnHand -= q`, `Reserved -= q`,
thêm biến động `Shipment`, chuyển reservation sang `Shipped` } → `200`.

### 8.6 Khách hủy đơn — `POST /api/orders/{id}/cancel`

Chỉ chủ đơn. Transaction { cập nhật có điều kiện `PendingPayment → Cancelled` → trả hàng đã giữ } → `200`.
Trạng thái khác → `409`.

---

## 9. Danh sách API

Mọi route có tiền tố `/api`. Mọi endpoint trả danh sách đều phân trang (`Rule.md` §3).

| Method | Route | Vai trò | Ghi chú |
|---|---|---|---|
| POST | `/auth/register` | Anonymous | Có rate limit |
| POST | `/auth/login` | Anonymous | Có rate limit |
| POST | `/auth/logout` | Mọi người đã đăng nhập | |
| GET | `/auth/me` | Mọi người đã đăng nhập | Id, tên, vai trò |
| GET | `/categories` | Anonymous | Có cache |
| GET | `/products` | Anonymous | `q`, `categoryId`, `brand`, `minPrice`, `maxPrice`, `sort`, `page`, `pageSize`; có cache |
| GET | `/products/{slug}` | Anonymous | Sản phẩm và các SKU; cache 60 giây |
| GET | `/products/{slug}/stock-status` | Anonymous | Trạng thái hàng theo từng SKU; cache ≤ 10 giây |
| GET | `/cart` | Customer | |
| PUT | `/cart/items/{skuId}` | Customer | Body `{ quantity }` |
| DELETE | `/cart/items/{skuId}` | Customer | |
| POST | `/orders` | Customer | Header `Idempotency-Key`; có rate limit |
| GET | `/orders` | Customer | Chỉ đơn của mình |
| GET | `/orders/{id}` | Customer | Chỉ đơn của mình; nếu không → `404` |
| POST | `/orders/{id}/cancel` | Customer | Đơn của mình, chỉ khi `PendingPayment` |
| POST | `/orders/{id}/payments` | Customer | Tạo payment giả lập; trả URL trang thanh toán |
| POST | `/payments/webhook` | Anonymous + chữ ký | |
| POST | `/mock-gateway/payments/{paymentId}/simulate` | Chỉ Development/Demo | `{ outcome, duplicate, delaySeconds }` |
| GET/POST/PUT | `/admin/categories[...]` | Admin | |
| GET/POST/PUT | `/admin/products[...]`, `/admin/products/{id}/skus[...]` | Admin | Xóa cache catalog |
| GET/POST/PUT | `/admin/warehouses[...]` | Admin (GET: cả WarehouseStaff) | |
| GET | `/admin/stock` | WarehouseStaff, Admin | Lọc theo kho, SKU, sắp hết hàng |
| POST | `/admin/stock/receipts` | WarehouseStaff, Admin | |
| POST | `/admin/stock/adjustments` | Admin | Bắt buộc có lý do |
| GET | `/admin/stock/movements` | WarehouseStaff, Admin | Phân trang keyset (`cursor`) |
| GET | `/admin/orders` | WarehouseStaff, Admin | Lọc theo trạng thái |
| POST | `/admin/orders/{id}/ship` | WarehouseStaff | Chỉ đơn `Paid`; Admin không được (tách nhiệm vụ, §3) |
| POST | `/admin/orders/{id}/complete` | WarehouseStaff | Chỉ đơn `Shipped`; Admin không được (§3) |
| GET | `/health/live` | Anonymous | Đường dẫn đầy đủ `/api/health/live`. Tiến trình đang chạy (Docker healthcheck); không bao giờ đụng tới database |
| GET | `/health/ready` | Anonymous | Đường dẫn đầy đủ `/api/health/ready`. Kết nối được database: `200`, ngược lại `503`; kiểm tra tối đa 3 giây (giám sát uptime, kiểm tra khi deploy) |
| POST | `/assistant/chat` | Customer | **Điểm cộng B1**; có rate limit; bật/tắt bằng feature flag |

**Mã trạng thái**

| Mã | Ý nghĩa |
|---|---|
| `200` / `201` | Thành công |
| `400` | Dữ liệu không hợp lệ |
| `401` | Chưa đăng nhập |
| `403` | Sai vai trò (hoặc thiếu header CSRF, §11.5) |
| `404` | Không tìm thấy, hoặc không phải của người gọi |
| `409` | Xung đột nghiệp vụ (hết hàng, sai trạng thái) |
| `429` | Bị giới hạn tần suất |

Mọi lỗi là phản hồi ProblemDetails có `code` cố định và `traceId` của request.
`title` và câu báo lỗi bằng tiếng Anh; frontend hiển thị câu tiếng Việt dựa trên `code`.
Các mã lỗi chung: `validation_failed` (400, kèm `errors` theo tên field dạng camelCase), `unauthorized` (401),
`forbidden` (403), `not_found` (404), `method_not_allowed` (405), `conflict` (409, dùng khi không có mã nghiệp vụ nào),
`rate_limited` (429), `internal_error` (500 và mọi 5xx khác, không bao giờ lộ chi tiết exception), `database_unavailable`
(503, lỗi database tạm thời), `service_unavailable` (503 khác), `bad_request` (mọi 4xx khác, ví dụ 415).
Các mã lỗi nghiệp vụ: `out_of_stock`, `invalid_status`, `too_many_pending_orders`, `idempotency_key_reused`,
`adjustment_below_reserved` (§7.10), `email_taken` (409, đăng ký), `invalid_credentials` (401, đăng nhập: email không
tồn tại, sai mật khẩu và tài khoản bị khóa trả lời giống hệt nhau), `csrf_header_missing` (403, §11.5).

**Định dạng phản hồi**

- Danh sách phân trang theo trang: `{ items, page, pageSize, totalCount }` (`pageSize` mặc định 20, tối đa 100).
- Danh sách phân trang keyset: `{ items, nextCursor }` (`nextCursor` là `null` ở trang cuối).
- Enum được trả về dạng chuỗi (ví dụ `"PendingPayment"`).
- Tài liệu API: tài liệu OpenAPI ở `/api/openapi/v1.json` và trang Scalar ở `/api/docs`, chỉ bật ở `Development` và `Demo`.
- Không đánh version API vì chỉ có một client do mình kiểm soát. Thay đổi phá vỡ tương thích phải cập nhật kiểu dữ liệu
  sinh ra cho frontend trong cùng pull request.

---

## 10. Thiết kế tìm kiếm (P6)

- `NameNormalized` được C# tính mỗi lần lưu sản phẩm: chuyển chữ thường → bỏ dấu
  (chuẩn hóa Unicode FormD, rồi bỏ các dấu kết hợp) → đổi `đ` thành `d` → gộp khoảng trắng.
- Cùng hàm đó được dùng để chuẩn hóa từ khóa người dùng gõ.
- Truy vấn: `NameNormalized ILIKE '%term%'` (hoặc toán tử độ tương tự `%`), dựa trên **GIN trigram index** (`pg_trgm`).
- Bộ lọc: danh mục (gồm cả danh mục con), thương hiệu, khoảng giá (giá SKU thấp nhất).
  Sắp xếp: liên quan nhất, giá tăng/giảm, mới nhất.
- Hàm chuẩn hóa có unit test với các trường hợp tiếng Việt (`Điện thoại` → `dien thoai`, `Đồng hồ` → `dong ho`).

---

## 11. Thiết kế phi chức năng

### 11.1 Mục tiêu và ước tính quy mô

Đây là **mục tiêu cần kiểm chứng, chưa phải kết quả đo được**. Sau lần chạy k6 đầu tiên, điều chỉnh lại và ghi vào §19.

| Mảng | Mục tiêu |
|---|---|
| Hạ tầng | 1 VPS (2 vCPU / 4 GB RAM), 1 instance API, 1 instance PostgreSQL |
| Đọc catalog | p95 < 200 ms ở 200 request/giây (có cache) |
| Đặt hàng | p95 < 500 ms ở 50 request/giây, tồn kho bình thường |
| Flash buy (L2) | 1.000 người mua đồng thời cho 1 sản phẩm → 1 thành công, không có lỗi nào ngoài `409`, mọi phản hồi trong 10 giây |
| Tính đúng đắn | 0 sản phẩm bị bán vượt và bất biến của sổ biến động luôn đúng, trong mọi bài test |
| Tính sẵn sàng | Một server, không có dự phòng (chấp nhận được với bản demo); container tự khởi động lại |
| Mức mất dữ liệu chấp nhận được (RPO) | Tối đa 24 giờ (backup hằng ngày) |
| Thời gian khôi phục (RTO) | < 1 giờ (khôi phục bản backup gần nhất) |

**Ước tính nhanh**

- 100.000 đơn × khoảng 2 dòng ≈ 300.000 dòng, khoảng 100 MB kể cả index. Sổ biến động tăng với tốc độ tương tự.
  Một instance PostgreSQL là dư sức; không cần sharding hay partitioning.
- Kết nối: pool của API `Maximum Pool Size=50`; PostgreSQL `max_connections=100`
  (chừa chỗ cho migration, backup và công cụ quản trị); thời gian chờ pool `Timeout=15` giây.
- Vì vậy 1.000 request đồng thời sẽ **xếp hàng chờ kết nối**. Đây là điều dự kiến, và mục tiêu 10 giây đã tính đến việc này.

### 11.2 Hiệu năng (P7)

- **Transaction ngắn.** Kiểm tra đầu vào, tính giá và chọn kho ứng viên diễn ra **trước** khi mở transaction đặt hàng
  (§8.1). Dòng tồn kho đã giữ bị khóa cho tới khi commit, nên mỗi mili giây thừa trong transaction đều làm chậm
  mọi người đang mua cùng SKU.
- **Cache catalog** bằng HybridCache: danh mục, các trang danh sách sản phẩm và chi tiết sản phẩm, hết hạn sau 60 giây.
  Thay đổi sản phẩm, SKU và giá sẽ xóa các key bị ảnh hưởng. Trạng thái hàng có endpoint riêng với cache ngắn (≤ 10 giây).
  Danh sách sản phẩm không hiển thị trạng thái hàng, nhờ đó việc cache danh sách đơn giản.
- **Không bao giờ** dùng tồn kho trong cache để quyết định giữ hàng có thành công hay không.
- **Các policy rate limiting**

  | Policy | Giới hạn |
  |---|---|
  | `auth` (đăng nhập, đăng ký) | 5 request/phút mỗi IP |
  | `checkout` | 10 request/phút mỗi người dùng |
  | `assistant` (điểm cộng) | 20 request/giờ mỗi người dùng |
  | Toàn cục | 100 request/10 giây mỗi IP |

  Request bị từ chối nhận `429` kèm header `Retry-After`.
- **Truy vấn:** `AsNoTracking`, projection bằng `Select`, phân trang và index trên các cột hay lọc.
- **Đo tải bằng k6** (script trong `tests/load/`):
  - `L1-catalog-spike.js` dùng executor `ramping-arrival-rate` của k6, tăng dần tới **200 request/giây** xem catalog
    và giữ khoảng 2 phút, khớp với mục tiêu ở §11.1; một giai đoạn tùy chọn khoảng 400 request/giây để tìm giới hạn.
    Chạy **trước** và **sau** khi thêm cache. Tải được đặt theo request/giây, không theo số người dùng ảo, vì mục tiêu
    được định nghĩa bằng request/giây. Điều chỉnh các con số sau lần chạy đầu (§11.1).
  - `L2-flash-buy.js` cho 1.000 người dùng ảo cùng mua một SKU chỉ còn 1 sản phẩm.
  - Ghi p95 latency, tỷ lệ lỗi và cấu hình máy vào `README.md`. Chỉ công bố số liệu đo thật.
  - k6 chạy với môi trường `LoadTest` (§11.6): tắt rate limiting (nếu không, mọi request của k6 dùng chung một IP
    và bị `429`), seed sẵn 1.000 tài khoản khách để test tải và đăng nhập trong bước `setup()` của k6.
  - k6 chạy từ **một máy khác** với server; chạy chung một máy sẽ làm sai số liệu.
  - Rate limiter được kiểm tra riêng: một integration test và một script k6 nhỏ mong đợi `429`.

### 11.3 Độ tin cậy và các tình huống lỗi

| Tình huống | Hành vi bắt buộc |
|---|---|
| API sập giữa lúc đặt hàng | Transaction chưa commit nên PostgreSQL tự hoàn tác: không giữ hàng, không có đơn |
| Bấm hai lần hoặc client gửi lại | Idempotency key (§7.7) |
| Webhook bị gửi trùng hoặc gửi lại | Loại trùng theo `ProviderTransactionId` (§7.9) |
| Webhook không bao giờ tới | Đơn hết hạn và hàng được trả lại; webhook tới muộn dẫn tới `RefundRequired` |
| Job hết hạn bị lỗi hoặc server khởi động lại | Mọi trạng thái nằm trong database; lần chạy sau tiếp tục |
| Tắt ứng dụng khi job đang chạy | Tôn trọng `CancellationToken`; mỗi đơn một transaction |
| Database không truy cập được | `/api/health/ready` báo lỗi; API trả `503` dạng ProblemDetails; ứng dụng vẫn khởi động được (không làm việc với database lúc khởi động, trừ việc Data Protection thử nạp khóa và chỉ ghi log lỗi, ADR 0004) |
| Deadlock hoặc lỗi serialization (`40P01`, `40001`) | Chạy lại toàn bộ transaction tối đa 3 lần, chờ một khoảng ngắn ngẫu nhiên (đặt hàng, xuất kho, hết hạn) |
| Đầy ổ cứng | Ngăn bằng xoay vòng log và giới hạn thời gian giữ backup (§11.6) |
| Deploy lỗi | Deploy lại image tag của phiên bản trước (§11.6) |

**Quy tắc database**

- Dùng mức cô lập mặc định của PostgreSQL, `READ COMMITTED`; câu cập nhật có điều kiện nguyên tử vẫn đúng ở mức này.
- **Không** bật `EnableRetryOnFailure` của EF Core cho toàn ứng dụng — nó xung đột với transaction mở thủ công.
  Chỉ thử lại một cách tường minh ở những chỗ liệt kê ở trên.
- Sau bất kỳ lỗi database nào bên trong transaction (ví dụ vi phạm ràng buộc duy nhất), transaction đã hỏng:
  rollback, rồi chạy các truy vấn tiếp theo bên ngoài nó.

### 11.4 Khả năng quan sát (observability)

- Log có cấu trúc được ghi ra stdout (`Rule.md` §6) và do Docker thu thập.
- Mỗi request có một trace id. Trace id xuất hiện trong mọi dòng log và trong mọi phản hồi ProblemDetails dưới dạng `traceId`,
  để khi người dùng báo lỗi có thể tìm đúng log.
- Ghi log các sự kiện nghiệp vụ: đơn được tạo / hết hạn / thanh toán / xuất kho, nhập kho / điều chỉnh kho, webhook bị từ chối.
- `/api/health/live` phục vụ healthcheck của Docker (chỉ khởi động lại khi tiến trình bị treo, không phải khi database lỗi);
  `/api/health/ready` phục vụ giám sát và bước kiểm tra khi deploy. Cả hai nằm dưới `/api`, nên Caddy chuyển tiếp được mà không cần thêm luật.
- Một dịch vụ giám sát uptime bên ngoài (dịch vụ miễn phí nào cũng được) kiểm tra `/api/health/ready` mỗi 5 phút và báo cho chủ dự án.
- Trace và metric bằng OpenTelemetry là tùy chọn, chỉ làm nếu còn thời gian (W5-02).

### 11.5 Thiết kế bảo mật

| Chủ đề | Thiết kế |
|---|---|
| Cookie đăng nhập | HttpOnly, Secure, `SameSite=Strict`, hết hạn trượt 7 ngày |
| Không chuyển hướng sang trang đăng nhập | Sự kiện của cookie trả `401` / `403` thay vì chuyển hướng mặc định của ASP.NET Core |
| CSRF | Ngoài `SameSite=Strict`, mọi request thay đổi dữ liệu (POST/PUT/PATCH/DELETE) phải gửi `X-CSRF: 1`; nếu không → `403`. Trình duyệt không thể thêm header tự đặt vào request cross-site nếu không qua CORS preflight, mà API không cho phép. Webhook được miễn vì đã có chữ ký. |
| Khóa Data Protection | Lưu trong PostgreSQL (`data_protection_keys`) để deploy lại không làm mất hiệu lực cookie |
| Identity | Mật khẩu ≥ 8 ký tự; khóa tài khoản 15 phút sau 5 lần đăng nhập sai; thông báo lỗi luôn là "email hoặc mật khẩu không đúng" |
| Reverse proxy | Middleware Forwarded Headers, chỉ tin container Caddy, để dùng đúng IP thật của khách (cho rate limiting và log) và đúng giao thức HTTPS. Nếu không, mọi người dùng chung IP của Caddy và chung một giới hạn tần suất. |
| Security headers (cấu hình ở Caddy) | HSTS, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, một Content-Security-Policy cơ bản |
| Webhook | Chữ ký + timestamp + so sánh thời gian cố định (§7.9) |
| Secret | user-secrets khi dev; trên VPS dùng biến môi trường từ file `.env` (quyền 600, không đưa vào git) |
| Thư viện phụ thuộc | CI chạy `dotnet list package --vulnerable` và `npm audit --audit-level=high`; bật Dependabot |
| Demo công khai | Bật rate limit; đặt giới hạn chi tiêu ở nhà cung cấp AI (điểm cộng B1) |

### 11.6 Môi trường, deploy và vận hành

| Môi trường | Mục đích | Khác biệt |
|---|---|---|
| `Development` | Làm việc trên máy | Bật cổng giả lập, dữ liệu demo, user-secrets |
| `Test` | Integration test | Testcontainers, cơ chế xác thực dành cho test (§13.2) |
| `LoadTest` | Chạy k6 | Tắt rate limiting, seed 1.000 tài khoản khách để test tải |
| `Demo` | Bản deploy công khai | Bật cổng giả lập, dữ liệu demo, hạn thanh toán 1 phút |

- **Cấu hình** dùng các lớp options được kiểm tra khi khởi động (`ValidateOnStart`), thiếu cấu hình là báo lỗi ngay.
- **CI** (mỗi pull request vào `main` và mỗi lần push lên `main`; push lên branch tính năng đã được kiểm tra qua pull request của nó): build, test, lint, kiểm tra lỗ hổng thư viện.
- **Image:** mỗi lần merge vào `main`, Docker image được build, gắn tag bằng SHA của commit git và đẩy lên GitHub Container Registry.
- **Deploy** (kích hoạt thủ công bằng `workflow_dispatch`, qua SSH): backup database → `docker compose pull` → chạy
  **migration bundle** của EF Core → chạy lệnh `seed` (idempotent, §13.4) → `docker compose up -d` → chờ `/api/health/ready`.
- **Migration** không bao giờ tự chạy khi ứng dụng khởi động. Migration phải tương thích ngược với phiên bản ứng dụng
  trước đó (thêm cột mới ở dạng nullable trước; chỉ xóa cột cũ ở lần phát hành sau), để quay lui được.
- **Quay lui:** deploy lại image tag của phiên bản trước. Khôi phục backup là phương án cuối cùng.
- **Backup:** `pg_dump` hằng ngày, giữ 7 ngày và sao chép ra ngoài VPS. **Thử khôi phục một lần trước khi demo.**
- Dữ liệu PostgreSQL nằm trong một Docker volume có tên. Mọi container dùng `restart: unless-stopped`.
- Xoay vòng log của Docker: `max-size: 10m`, `max-file: 3`.
- **Hosting** (quyết ở W5-03): ưu tiên một VM miễn phí (ví dụ Oracle Cloud Always Free, cấu hình 2 vCPU / 4 GB cho khớp
  §11.1) cùng một tên miền con miễn phí (ví dụ DuckDNS); dự phòng là thuê VPS giá rẻ. Cách nào cũng cần tên miền:
  cookie đăng nhập có cờ `Secure` (§11.5) nên site phải chạy HTTPS, và Caddy cần tên miền để lấy chứng chỉ.

### 11.7 Giới hạn khi mở rộng (đã biết và chấp nhận)

Dự án giả định **chỉ có một instance API**. Bảng dưới liệt kê những gì sẽ hỏng khi chạy từ hai instance trở lên, và cách sửa.

| Thành phần | Vấn đề khi có nhiều instance | Cách sửa (giải thích, không làm) |
|---|---|---|
| Cache trong bộ nhớ | Xóa cache ở một instance không lan tới các instance khác | Dùng Redis làm cache chung, hoặc thời gian hết hạn rất ngắn |
| Rate limiter | Mỗi instance đếm riêng | Rate limiter dựa trên Redis, hoặc giới hạn ở proxy/gateway |
| Job hết hạn | Chạy trên mọi instance | Vốn đã an toàn (cập nhật có điều kiện); thêm `FOR UPDATE SKIP LOCKED` để tránh làm trùng việc |
| Khóa Data Protection | — | Đã dùng chung qua database |
| Hàng đợi của cổng giả lập | Nằm trong bộ nhớ; mất khi khởi động lại | Chấp nhận được với bản giả lập |

---

## 12. Frontend

### 12.1 Website bán hàng (`src/web/storefront`)

| Trang | Ghi chú |
|---|---|
| Trang chủ / danh sách sản phẩm | Menu danh mục, ô tìm kiếm, bộ lọc, phân trang (không hiện trạng thái hàng) |
| Chi tiết sản phẩm | Chọn SKU, giá, trạng thái hàng (request riêng), thêm vào giỏ |
| Giỏ hàng | Số lượng, giá hiện tại, cảnh báo tồn kho |
| Thanh toán | Form giao hàng (Zod); đặt hàng kèm idempotency key; khóa nút gửi khi đang chờ; xử lý mọi mã lỗi `409` |
| Trang thanh toán giả lập | Các nút: Thành công / Thất bại / Webhook trùng / Gửi trễ; sau đó polling trạng thái đơn |
| Đơn của tôi, Chi tiết đơn | Trạng thái, đếm ngược theo `ExpiresAt` − `ServerNow`, nút hủy |
| Đăng nhập / Đăng ký | |
| Khung chat | **Điểm cộng B1** |

Mọi request thay đổi dữ liệu đều gửi header `X-CSRF: 1`, cài một lần trong API client dùng chung.

### 12.2 Trang quản trị (`src/web/admin`)

| Trang | Vai trò |
|---|---|
| Sản phẩm và SKU | Admin |
| Danh mục | Admin |
| Kho | Admin |
| Tổng quan tồn kho (theo kho, lọc sắp hết hàng) | WarehouseStaff, Admin |
| Form nhập kho | WarehouseStaff, Admin |
| Form điều chỉnh tồn kho | Admin |
| Sổ biến động (theo SKU/kho) | WarehouseStaff, Admin |
| Danh sách đơn kèm thao tác Xuất kho / Hoàn tất | WarehouseStaff, Admin |

Quy tắc giao diện: xử lý trạng thái đang tải, rỗng và lỗi; thông báo rõ ràng cho `409` (hết hàng, sai trạng thái) và `429`.
Thiết kế đơn giản (Tailwind); không tốn thời gian cho animation.

---

## 13. Chiến lược kiểm thử

### 13.1 Unit test

- Chuẩn hóa văn bản tiếng Việt
- Tính tiền từng dòng và tổng tiền
- Quy tắc chuyển trạng thái đơn hàng
- Validator
- Kiểm tra chữ ký webhook

### 13.2 Integration test (Testcontainers + PostgreSQL)

Phần lớn test dùng **cơ chế xác thực dành cho test**: một header trong request chọn người dùng và vai trò, để các test
chạy song song không tốn thời gian băm mật khẩu. Một vài test dùng đăng nhập bằng cookie thật để kiểm tra luồng này.

| Mảng | Trường hợp |
|---|---|
| Xác thực | `401` khi chưa đăng nhập (không phải chuyển hướng `302`); `403` khi sai vai trò ở endpoint admin; khóa tài khoản sau 5 lần đăng nhập sai |
| CSRF | POST thiếu `X-CSRF` → `403` |
| IDOR | Khách B xem đơn của khách A → `404` |
| Đặt hàng | Thành công; hết hàng → `409`; giá do client gửi bị bỏ qua; phản hồi có `ServerNow` |
| Idempotency | Gửi hai lần cùng `Idempotency-Key` → một đơn, cùng phản hồi; cùng key nhưng giỏ khác → `409 idempotency_key_reused` |
| Giới hạn đơn chờ thanh toán | Đơn chưa thanh toán thứ tư → `409 too_many_pending_orders` |
| Kho | Nhập kho và điều chỉnh đều ghi biến động; điều chỉnh giảm xuống dưới `Reserved` → `409 adjustment_below_reserved`; xuất kho cập nhật `OnHand`, `Reserved` và sổ biến động |
| Webhook | Sai chữ ký → `401`; timestamp cũ hơn 5 phút → `401`; sai số tiền → payment `Failed` |
| Lỗi | Phản hồi lỗi có `traceId` |
| Tìm kiếm | `dien thoai` ra `Điện thoại` |

### 13.3 Test đồng thời (bắt buộc; xem `Rule.md` §9)

| ID | Kịch bản | Kết quả mong đợi |
|---|---|---|
| **C1** | 200 lượt đặt hàng song song (khách khác nhau) cho SKU chỉ còn 1 sản phẩm | Đúng 1 thành công, 199 × `409`; `Reserved == 1`; `OnHand - Reserved >= 0` |
| **C2** | 50 lượt đặt hàng song song cùng một `Idempotency-Key` | Đúng 1 đơn |
| **C3** | Cùng một webhook thành công gửi song song 10 lần | 1 payment được áp dụng; đơn chuyển `Paid` một lần |
| **C4** | Job hết hạn và webhook thành công tranh nhau trên cùng một đơn | Chỉ một trong hai: `Paid` hoặc `Expired`; tồn kho nhất quán; thanh toán trễ → `RefundRequired` |
| **C5** | Ngẫu nhiên nhiều thao tác: nhập kho, điều chỉnh, đặt hàng, hủy, xuất kho | Với mọi dòng tồn kho: `SUM(QuantityDelta) == OnHand` và `0 <= Reserved <= OnHand` |

C1 dùng 200 request để chạy được trên CI; phiên bản 1.000 người dùng chạy bằng k6 (`L2-flash-buy.js`).

### 13.4 Dữ liệu mẫu (Bogus, seed cố định)

- 4 danh mục cấp 1 — Điện thoại, Laptop, Linh kiện máy tính, Phụ kiện — cùng vài danh mục con.
- Khoảng 40 sản phẩm và 120 SKU, thương hiệu hư cấu, tên sản phẩm tiếng Việt có dấu.
- 3 kho: `HN01` (Hà Nội), `HCM01` (TP.HCM), `DN01` (Đà Nẵng).
- **Tồn kho ban đầu được tạo qua service nhập kho**, không bao giờ insert thẳng vào `stock_items`,
  để bất biến của sổ biến động (`SUM(QuantityDelta) == OnHand`) đúng ngay từ đầu.
- Seed có tính idempotent: chạy hai lần không thay đổi gì.
- Chỉ ở `LoadTest`: 1.000 tài khoản khách, `load-0001@test.local` … `load-1000@test.local`.
- Tài khoản demo (chỉ Development và Demo): `customer1@demo.local`, `customer2@demo.local`, `staff@demo.local`,
  `admin@demo.local`. Mật khẩu demo được đặt qua cấu hình và ghi trong README.
- Một SKU chỉ còn đúng 1 sản phẩm, dùng cho demo flash buy.

**Cách chạy seed.** Project API có chế độ dòng lệnh `seed`: khi khởi động với tham số này, ứng dụng seed database rồi
thoát, không khởi động web server. Khi dev thì chạy tay; khi deploy thì chạy như một bước ngay sau migration bundle
(§11.6). Nhờ vậy lúc khởi động không làm việc với database (§11.3). Bộ seed chạy bên trong ứng dụng, không phải
script SQL, vì tồn kho ban đầu bắt buộc phải đi qua service nhập kho.

| Task | Seed gì |
|---|---|
| W1-05 | Các vai trò và tài khoản `staff@demo.local` / `admin@demo.local` (tạo lệnh `seed`) |
| W1-08 | Danh mục, sản phẩm, SKU và các khách demo |
| W2-03 | Tồn kho ban đầu, qua service nhập kho |
| W4-07 | 1.000 tài khoản khách của `LoadTest` |

---

## 14. Lộ trình và danh sách task

**Ký hiệu:** ⭐ đánh dấu task cốt lõi. Chủ dự án nên tự viết trước, hoặc ít nhất hiểu hoàn toàn (`CLAUDE.md` §7).
Mọi task đi theo quy trình đầy đủ ở `Rule.md` §1.

**Theo dõi bằng GitHub Issues**

- Danh sách này là **kế hoạch**. Một task có **GitHub issue** (spec của task), do `/to-spec` tạo, khi bắt đầu làm task đó.
  Issue không được tạo trước, nên kế hoạch và issue không thể lệch nhau.
- Khi đã có issue, thêm số issue sau mã task, ví dụ `**W3-03** (#14) Đặt hàng ...`.
- Mỗi issue thuộc milestone GitHub của tuần tương ứng (`Week 1` … `Week 5`, `Bonus`).
- Task xong khi pull request (`Closes #14`) được merge. Lúc đó đánh dấu ở đây và trong `Workflow.md`.

**Giai đoạn hiện tại:** Tuần 1 — đang thực hiện

### Tuần 1 — Nền tảng và Catalog

- [x] **W1-01** (#6) Dựng repository: solution, thư mục theo `Rule.md` §2, `.editorconfig`, `Directory.Build.props`
  (bật nullable, coi cảnh báo là lỗi), `docker-compose.yml` (PostgreSQL), `.gitignore`, khung README.
  *Xong khi:* chạy `docker compose up -d` rồi `dotnet run` thì API khởi động được.
- [x] **W1-02** (#8) CI bằng GitHub Actions: build và test backend, kiểm tra lỗ hổng thư viện .NET; Dependabot.
  Job cho frontend được thêm ở W1-09, khi các app đã tồn tại.
  *Xong khi:* một pull request hiện dấu tích xanh.
- [x] **W1-03** (#10) Nền tảng dữ liệu: `AppDbContext`, đặt tên snake_case, quy ước UTC và decimal, `TimeProvider`,
  cấu hình connection pool, kiểm tra options khi khởi động, migration đầu tiên, `/api/health/live` và `/api/health/ready`.
  Đồng thời gỡ tùy chọn CI `--ignore-exit-code 8` khi đã có test đầu tiên (từ W1-02, #8).
- [x] **W1-04** (#12) Xử lý lỗi và tài liệu API: ProblemDetails có `code` và `traceId`, nối FluentValidation,
  kiểu phản hồi danh sách chuẩn (§9), OpenAPI + Scalar.
- [ ] **W1-05** (#14) Identity: đăng ký, đăng nhập, đăng xuất, me; cấu hình cookie (§11.5); `401`/`403` thay vì chuyển hướng;
  kiểm tra header CSRF; khóa tài khoản; khóa Data Protection trong database; lệnh `seed` cùng các vai trò và tài khoản
  WarehouseStaff/Admin (§13.4); policy phân quyền; cơ chế xác thực dành cho integration test.
  *Xong khi:* có test chứng minh các trường hợp `401`, `403`, thiếu CSRF và khóa tài khoản.
- [ ] **W1-06** Quản trị Catalog: danh mục, sản phẩm, SKU (DTO, validator, sinh slug, cập nhật `MinPrice`).
- [ ] **W1-07** Catalog công khai: danh sách sản phẩm (lọc, phân trang) và chi tiết sản phẩm.
- [ ] **W1-08** Dữ liệu mẫu bằng Bogus (§13.4): danh mục, sản phẩm, SKU và khách demo. **Chưa có tồn kho** (thêm ở W2-03).
- [ ] **W1-09** Dựng frontend: hai app Vite, routing, Tailwind, TanStack Query, sinh kiểu API,
  trang đăng nhập, danh sách và chi tiết sản phẩm (storefront), quản lý sản phẩm (admin); thêm job lint, build và
  `npm audit` cho frontend vào CI.
  *Xong khi:* CI kiểm tra cả hai app frontend.

### Tuần 2 — Kho

- [ ] **W2-01** Kho (admin).
- [ ] **W2-02** Bảng `stock_items` với ràng buộc CHECK và unique index.
- [ ] ⭐ **W2-03** Nhập kho, ghi biến động trong cùng transaction; mở rộng seed để tạo tồn kho ban đầu qua service này.
- [ ] **W2-04** Điều chỉnh tồn kho (chỉ Admin, bắt buộc lý do, không giảm xuống dưới `Reserved`).
- [ ] **W2-05** Tổng quan tồn kho và truy vấn sổ biến động (phân trang keyset).
- [ ] **W2-06** Endpoint trạng thái hàng (`InStock` / `LowStock` / `OutOfStock`) và hiển thị ở trang chi tiết sản phẩm.
- [ ] **W2-07** Giao diện admin: kho, tổng quan tồn kho, form nhập kho, form điều chỉnh, trang sổ biến động.

### Tuần 3 — Giỏ hàng và đặt hàng

- [ ] **W3-01** API giỏ hàng.
- [ ] ⭐ **W3-02** Service giữ hàng của Inventory: `TryReserve`, `Release`, `Commit`, `Ship` (cập nhật nguyên tử, §7.2).
- [ ] ⭐ **W3-03** Đặt hàng (§8.1): chọn kho, snapshot giá, sequence mã đơn,
  đơn + dòng đơn + reservation + dọn giỏ trong một transaction, idempotency key và request hash,
  giới hạn đơn chờ thanh toán, thử lại khi gặp deadlock/lỗi serialization.
- [ ] **W3-04** Danh sách và chi tiết đơn của tôi, có kiểm tra quyền sở hữu (test IDOR).
- [ ] **W3-05** Khách hủy đơn.
- [ ] ⭐ **W3-06** Test đồng thời C1 và C2.
- [ ] **W3-07** Storefront: trang giỏ hàng, thanh toán, đơn của tôi và chi tiết đơn.

### Tuần 4 — Thanh toán, hết hạn, tìm kiếm, hiệu năng

- [ ] **W4-01** Bắt đầu thanh toán + cổng giả lập (chỉ Development/Demo, chỉ qua HTTP, gửi nền có thử lại,
  kiểm tra quyền sở hữu) + trang thanh toán giả lập có polling trạng thái.
- [ ] ⭐ **W4-02** Webhook thanh toán: chữ ký kèm timestamp, loại trùng, kiểm tra số tiền, đánh dấu đã thanh toán; test C3.
- [ ] ⭐ **W4-03** Job nền hết hạn giữ hàng; thanh toán trễ → `RefundRequired` → `Refunded`; test C4.
- [ ] **W4-04** Xuất kho và hoàn tất đơn (WarehouseStaff); test C5.
- [ ] **W4-05** Tìm kiếm tiếng Việt: chuẩn hóa, trigram index, bộ lọc.
- [ ] **W4-06** Cache catalog và các policy rate limiting.
- [ ] **W4-07** Môi trường `LoadTest` với 1.000 tài khoản test tải; chạy script k6 L1 và L2 từ máy khác;
  đo trước/sau và so với mục tiêu ở §11.1; ghi kết quả vào README.

### Tuần 5 — Hoàn thiện và ra mắt

- [ ] **W5-01** Hoàn thiện giao diện: trạng thái đang tải, rỗng và lỗi; thông báo cho `409` và `429`.
- [ ] **W5-02** Rà soát khả năng quan sát (§11.4): log có cấu trúc, che dữ liệu, trace id, sự kiện nghiệp vụ, giám sát uptime.
  OpenTelemetry chỉ làm nếu còn thời gian.
- [ ] **W5-03** Dockerfile và file compose cho production, Caddy (HTTPS, security headers), forwarded headers,
  image trên GitHub Container Registry, workflow deploy thủ công có migration bundle, backup hằng ngày
  **và một lần thử khôi phục**, xoay vòng log, seed môi trường Demo (§11.5, §11.6).
- [ ] **W5-04** Hoàn thiện README (§16); kiểm tra đủ mọi ADR ở §17; ảnh chụp màn hình hoặc GIF demo ngắn.
- [ ] **W5-05** Tập dượt kịch bản demo (§15); rà soát lần cuối toàn bộ code theo `Rule.md`.

### Tuần 6 — Điểm cộng B1: trợ lý AI (tùy chọn; chỉ sau Tuần 1–5)

- [ ] **B1-01** Các tool chỉ đọc: tìm sản phẩm, xem trạng thái hàng, xem trạng thái đơn của tôi.
- [ ] **B1-02** `/api/assistant/chat` dùng Microsoft.Extensions.AI, có rate limit và feature flag.
- [ ] **B1-03** Khung chat trên storefront, hiển thị thẻ sản phẩm (link do backend tạo).
- [ ] **B1-04** Bộ đánh giá: 30 câu hỏi kèm đáp án mong đợi, chạy được như một test.

**Quy tắc cho AI:** tool chỉ được đọc; id người dùng chỉ lấy từ ngữ cảnh đăng nhập; link, giá và trạng thái hàng
lấy từ dữ liệu backend, không bao giờ do mô hình tự tạo; mọi văn bản đều được coi là đầu vào không đáng tin (`Rule.md` §11).

**Nếu hết thời gian:** dừng sau Tuần 4, rồi làm W5-03 và W5-04 (deploy và README). Kết quả vẫn là một dự án hoàn chỉnh.

---

## 15. Kịch bản demo (khoảng 3 phút)

1. Mở trang tồn kho admin: SKU `NOVAX-128-BLK` còn 1 sản phẩm ở `HCM01`.
2. Chạy `k6 run tests/load/L2-flash-buy.js` (1.000 người mua) → 1 thành công, 999 phản hồi hết hàng; tồn kho không âm.
3. Đặt một đơn mà không thanh toán → sau thời hạn (1 phút ở Demo) đơn chuyển `Expired` và hàng bán được trở lại.
4. Ở trang thanh toán giả lập, bấm "Webhook trùng" → đơn chỉ được thanh toán một lần và chỉ có một dòng payment.
5. WarehouseStaff xuất kho đơn hàng → mở sổ biến động: mọi con số đều được giải thích bằng một dòng biến động.
6. Đăng nhập bằng khách khác và mở URL đơn của khách đầu tiên → `404`.
7. Tìm `dien thoai` → ra kết quả tiếng Việt.
8. (Điểm cộng) Hỏi trợ lý "Nova X 128GB màu đen còn hàng không?" → câu trả lời kèm thẻ sản phẩm và link.

---

## 16. Checklist README

- [ ] Mô tả một đoạn và cam kết cốt lõi
- [ ] Link demo và tài khoản demo cho từng vai trò
- [ ] Sơ đồ kiến trúc và sơ đồ trạng thái đơn/tồn kho
- [ ] "Bài toán khó và cách giải quyết": P1–P7, mỗi bài 3–4 dòng
- [ ] Kết quả test: test đồng thời và số liệu k6 kèm cấu hình máy
- [ ] Cách chạy cục bộ bằng một lệnh
- [ ] Công nghệ sử dụng
- [ ] Giới hạn đã biết và cách xử lý lỗi (§11.3, §11.7)
- [ ] Hướng phát triển khi quy mô lớn hơn (§18)

---

## 17. Tài liệu trong repository

| Cái gì | Ở đâu | Tạo bởi |
|---|---|---|
| Spec của mỗi task | GitHub issue (theo mẫu của skill `to-spec`) | `/grill-with-docs` → `/to-spec` |
| Thuật ngữ nghiệp vụ | `GLOSSARY.md` | `/grill-with-docs` (domain modeling) |
| Quyết định thiết kế | `docs/adr/NNNN-title.md` (bối cảnh, quyết định, phương án khác, hệ quả) | `/grill-with-docs`, hoặc ngay khi ra quyết định |
| Cấu hình cho agent skills | `docs/agents/*.md` | `/setup-matt-pocock-skills` (một lần) |

Mọi spec issue ghi `Task: W?-??` và các mục liên quan trong `Workflow.md` ở phần "Further Notes".

Các ADR cần viết (chậm nhất là khi xong task liên quan):

1. `0001-atomic-conditional-update-for-stock.md` — so sánh với khóa bi quan (pessimistic locking) và concurrency token lạc quan (optimistic)
2. `0002-append-only-stock-ledger.md`
3. `0003-idempotency-key-for-checkout.md`
4. `0004-cookie-auth-instead-of-jwt-in-browser.md`
5. `0005-no-redis-or-message-queue-yet.md`
6. `0006-csrf-protection-for-cookie-auth.md`
7. `0007-single-instance-deployment-and-backups.md`
8. `0008-github-issues-and-agent-skills-workflow.md`

(Issue, ADR và glossary viết bằng tiếng Anh để AI đọc thống nhất; có thể bình luận thêm bằng tiếng Việt nếu cần.)

---

## 18. Ý chính khi phỏng vấn: cách mở rộng (giải thích, không làm)

| Câu hỏi | Hướng trả lời |
|---|---|
| Lưu lượng tăng gấp 100 lần? | Thêm instance API (stateless), chuyển cache sang Redis, read replica cho catalog, CDN cho frontend |
| Flash sale thật? | Bộ đếm Redis làm cổng chặn trước database, hàng đợi xử lý đơn tuần tự, cache cờ "hết hàng" |
| Cổng thanh toán bị lỗi? | Retry có backoff, circuit breaker, job đối soát hỏi lại cổng thanh toán các giao dịch bị sót |
| Gửi sự kiện tin cậy (email, thống kê)? | Bảng outbox ghi trong cùng transaction, cộng message broker (RabbitMQ) |
| Đơn cần hàng từ 2 kho? | Tách thành một kiện hàng cho mỗi kho, mỗi kiện có trạng thái riêng |
| Vì sao chọn modular monolith? | Dễ xây dựng, deploy và test; có thể tách module ra sau nếu thật sự cần |

---

## 19. Nhật ký thay đổi kế hoạch

| Ngày | Thay đổi | Người duyệt |
|---|---|---|
| 2026-11-21 | Kế hoạch ban đầu | Chủ dự án |
| 2026-11-21 | Dùng mattpocock/skills: spec là GitHub issue (tạo khi bắt đầu từng task), `GLOSSARY.md`, `docs/adr/`, test trước theo từng lát dọc, `code-review` độc lập, chủ dự án tự push và merge | Chủ dự án |
| 2026-11-21 | Rà soát thiết kế hệ thống: mục tiêu và quy mô, độ tin cậy, khả năng quan sát, bảo mật, deploy và vận hành (§11); index và sequence mã đơn (§6.6); request hash cho idempotency, giới hạn đơn chờ thanh toán, chống gửi lại webhook và cơ chế gửi lại, dọn giỏ trong transaction đặt hàng; seed tồn kho qua nhập kho; trạng thái hàng thành endpoint riêng | Chủ dự án |
| 2026-11-21 | Đổi tên sản phẩm thành Gearnix (repository `gearnix`); tiền tố mã đơn `GN-` (trước đây ghi nhầm 2026-11-22 do lệch múi giờ) | Chủ dự án |
| 2026-11-21 | Viết lại văn phong `Workflow.md` và `Workflow.vi.md`: chỉ sửa câu chữ, cấu trúc và bảng; không đổi phạm vi hay quy tắc; câu hỏi còn mở được đánh dấu **[Cần xác nhận]** và gom ở §20 | Chủ dự án |
| 2026-11-23 | Chốt các câu hỏi còn mở: chỉ WarehouseStaff xuất kho/hoàn tất đơn (§3); thêm các package đã duyệt vào §4; thay `CancelReason` bằng `ExpiredAt` (§6.3); webhook mang `paymentId` (§7.9); mã lỗi `adjustment_below_reserved` (§9); tải L1 đặt theo request/giây (§11.2); hosting quyết ở W5-03 (§11.6); lệnh `seed` và seed theo từng task (§13.4, §11.6); job CI frontend chuyển sang W1-09 (§14) | Chủ dự án |
| 2026-11-26 | Thu hẹp điều kiện chạy CI: pull request vào `main` và push lên `main` (§4, §11.6); W1-03 gỡ tùy chọn tạm cho phép "không có test nào chạy" trong CI (§14) | Chủ dự án |
| 2026-11-26 | Duyệt Microsoft.AspNetCore.Mvc.Testing cho integration test (§4); ghi rõ đường dẫn đầy đủ `/api/health/...` của các endpoint health (§9, §11.3, §11.4, §11.6) | Chủ dự án |
| 2026-11-26 | Mã lỗi chung (gồm các mã dự phòng `conflict`, `bad_request`, `service_unavailable`), câu báo lỗi tiếng Anh do frontend dịch, enum dạng chuỗi, đường dẫn tài liệu API (§9) | Chủ dự án |
| 2026-11-26 | Identity: mã lỗi `email_taken`, `invalid_credentials`, `csrf_header_missing` (§9); bắt đăng nhập mặc định cho mọi controller thay vì fallback policy, để route không tồn tại vẫn trả `404`; Data Protection nạp khóa từ database lúc khởi động (§11.3) (#14) | Chủ dự án |

---

## 20. Câu hỏi còn mở

Khi một câu hỏi về kế hoạch chưa chốt được ngay, đánh dấu **[Cần xác nhận]** tại chỗ xuất hiện và thêm vào đây.
Khi đã chốt với chủ dự án, cập nhật mục liên quan, bỏ nhãn và dòng tương ứng, rồi ghi quyết định vào §19.

| # | Mục | Câu hỏi |
|---|---|---|
| — | — | Không còn câu hỏi mở (đã chốt hết ngày 2026-11-23; xem §19) |
