# PROMPT CODE — MODULE QUẢN LÝ ADMIN (Nhà hàng / Đặt món QR)

> Stack: **tùy chọn** (framework-agnostic). Hãy tự chọn ngôn ngữ/framework backend (Node.js, Java Spring Boot, PHP Laravel, .NET, Python Django/FastAPI...) và frontend (React, Vue, Angular, Blade...) phù hợp với dự án hiện có. Prompt dưới đây mô tả **nghiệp vụ, luồng xử lý, validate, message lỗi** để AI/bạn có thể sinh code cho bất kỳ stack nào.

---

## 0. Bối cảnh chung

Xây dựng các chức năng cho **trang Quản trị (Admin)** của hệ thống đặt món ăn qua mã QR tại bàn, gồm 9 use case:

| Mã UC | Chức năng |
|---|---|
| UC16 | Quản lý danh mục món ăn (xem/thêm/sửa/xóa) |
| UC17 | Thêm món ăn mới |
| UC18 | Sửa món ăn |
| UC19 | Xóa món ăn |
| UC20 | Xem danh sách món ăn |
| UC21 | Thêm bàn ăn mới |
| UC22 | Xóa bàn ăn |
| UC23 | Tạo mã QR cho bàn ăn |
| UC24 | Thống kê doanh thu |
| UC25 | Tạo sự kiện giảm giá theo % sản phẩm |

**Actor duy nhất:** Admin (đã đăng nhập, có quyền tương ứng — cần middleware/guard kiểm tra role `admin` cho toàn bộ các API/route bên dưới).

**Yêu cầu chung cho mọi API:**
- Xác thực đăng nhập (JWT/session) + kiểm tra quyền Admin trước khi xử lý.
- Validate dữ liệu đầu vào ở cả frontend (form) lẫn backend (API).
- Trả về mã lỗi/message rõ ràng theo bảng "System Message" của từng UC (dùng làm `errorCode` trong response JSON, ví dụ `{ "success": false, "code": "MS01", "message": "..." }`).
- Không thay đổi dữ liệu khi có lỗi (transaction rollback nếu dùng DB quan hệ).
- Ưu tiên **Soft Delete** (thêm cột `is_deleted` / `status`) cho món ăn và bàn ăn thay vì xóa cứng.

---

## 1. UC16 — Quản lý danh mục món ăn

**Entity gợi ý `Category`:**
```
id, name (unique, max 100 ký tự), description, created_at, updated_at
```

**API:**
- `GET /api/admin/categories` — xem danh sách
- `POST /api/admin/categories` — thêm mới
- `PUT /api/admin/categories/:id` — sửa
- `DELETE /api/admin/categories/:id` — xóa

**Business rules:**
- BR01.1: `name` không được để trống → thiếu thì `MS02`.
- BR01.2: `name` là duy nhất (không phân biệt hoa/thường nên chuẩn hóa trước khi so sánh) → trùng thì `MS01`.
- BR01.3: **không được xóa** danh mục đang chứa món ăn (`COUNT(dishes WHERE category_id = :id AND is_deleted = false) > 0`) → nếu có thì chặn, trả `MS03`.
- BR01.4: `name` tối đa 100 ký tự (validate cả frontend lẫn backend).

**Luồng xử lý:**
1. **Xem danh sách (main flow):** Admin vào menu Quản lý danh mục → hệ thống truy vấn và hiển thị toàn bộ danh mục.
2. **Thêm danh mục (Alt Flow 1):** nhập `name` + `description` → nhấn Lưu → validate (tên trống → `MS02`; tên trùng → `MS01`) → lưu DB → `MS04` (thêm thành công).
3. **Sửa danh mục (Alt Flow 3):** chọn danh mục → hiển thị dữ liệu hiện tại → chỉnh sửa → validate tương tự → cập nhật DB → thông báo thành công.
4. **Xóa danh mục (Alt Flow 4):** chọn danh mục → nhấn Xóa → hiển thị dialog xác nhận → Admin xác nhận → kiểm tra danh mục có món ăn không (BR01.3) → nếu không có thì xóa khỏi DB và trả thông báo xóa thành công; nếu có thì từ chối và trả `MS03`.

**System Message:** `MS01` tên danh mục đã tồn tại · `MS02` vui lòng nhập tên danh mục · `MS03` không thể xóa vì đang chứa món ăn · `MS04` thêm danh mục thành công.

> Lưu ý: UC17 (Thêm món ăn) phụ thuộc vào UC16 — nếu chưa có danh mục nào thì UC17 sẽ trả `MS05` (xem mục 2 bên dưới), nên nên code/triển khai UC16 trước.

---

## 2. UC17 — Thêm món ăn mới

**Entity gợi ý `Dish`:**
```
id, name, description, price, category_id, image_url,
customization_options (json, optional), status (active/inactive),
created_at, updated_at
```

**API:** `POST /api/admin/dishes`

**Input:** `name, description, price, category_id, customizations?, image (file upload)`

**Business rules cần code:**
- BR02.1: `name` bắt buộc, không rỗng.
- BR02.2: `price >= 0` và đúng định dạng số.
- BR02.3: `category_id` bắt buộc, phải tồn tại trong bảng `categories`.
- BR02.4: Ảnh chỉ nhận `jpg, png, webp`.
- BR02.5: Dung lượng ảnh ≤ 10MB.
- BR02.6: `name` không được trùng trong cùng `category_id` (unique composite check, không phân biệt hoa/thường nên tốt nhất).

**Luồng xử lý (main flow):**
1. Validate input theo thứ tự: thiếu field bắt buộc → `MS01`; giá không hợp lệ → `MS02`; tên trùng trong danh mục → `MS03`; ảnh sai định dạng/dung lượng → `MS04`.
2. Nếu hệ thống chưa có danh mục nào → `MS05` (chặn trước khi hiển thị form, kiểm tra ở bước load trang/list category).
3. Nếu hợp lệ: upload ảnh lên storage (local/S3/Cloudinary...), lưu record vào DB, trả về `MS06` + object món ăn vừa tạo.

**Response codes cần implement:** MS01–MS06 (xem bảng System Message gốc để lấy đúng nội dung tiếng Việt).

---

## 3. UC18 — Sửa món ăn

**API:** `PUT /api/admin/dishes/:id`

**Business rules:**
- BR03.1: món ăn phải tồn tại (chưa bị xóa) → nếu không tìm thấy trả `MS01`.
- BR03.2: `name` không rỗng → `MS02`.
- BR03.3: `price >= 0` → `MS03`.
- BR03.4: `name` không trùng trong cùng category (loại trừ chính bản ghi đang sửa) → `MS04`.
- BR03.5: ảnh hợp lệ (jpg/png/webp) → `MS05`.
- BR03.6: nếu không upload ảnh mới → giữ nguyên `image_url` cũ (không được set null).

**Luồng xử lý:**
1. Load dish theo `id`; nếu không tồn tại → `MS01`.
2. Validate các field theo thứ tự trên.
3. Update DB, trả `MS06` (cập nhật thành công).

---

## 4. UC19 — Xóa món ăn

**API:** `DELETE /api/admin/dishes/:id`

**Business rules:**
- BR04.2: kiểm tra tồn tại → không có thì `MS01`.
- BR04.3: kiểm tra món ăn có đang nằm trong đơn hàng **chưa hoàn tất** (`order_status NOT IN ('completed','cancelled')`) → nếu có thì chặn, trả `MS02`.
- BR04.4: yêu cầu xác nhận phía frontend (dialog confirm, message `MS04`) trước khi gọi API xóa thật.
- BR04.6: khuyến khích Soft Delete — set `status = 'deleted'` hoặc `is_deleted = true` thay vì xóa row.
- Nếu lỗi DB khi xóa/update → `MS03`.
- Thành công → `MS05`, món ăn không còn hiển thị ở menu khách hàng (thêm điều kiện filter `status != 'deleted'` ở API menu phía client).

---

## 5. UC20 — Xem danh sách món ăn

**API:** `GET /api/admin/dishes?keyword=&category_id=&page=&limit=&sort=`

**Business rules:**
- BR05.2: sắp xếp mặc định theo tên hoặc `created_at`.
- BR05.3: tìm kiếm theo tên, **không phân biệt hoa thường** (dùng `ILIKE`/`LOWER()` tùy DB).
- BR05.4: lọc theo `category_id`.
- BR05.5: phân trang (`page`, `limit`, trả kèm `total`, `totalPages`).
- BR05.6: response chỉ trả các action (edit/delete) mà role hiện tại được phép — chuẩn bị sẵn field `permissions` nếu sau này có nhiều role hơn Admin.

**Các trường hợp trả message riêng (không phải lỗi HTTP, chỉ là trạng thái rỗng):**
- Không có món ăn nào → `MS01`.
- Tìm kiếm không ra kết quả → `MS02`.
- Danh mục được chọn chưa có món → `MS03`.
- Lỗi kết nối DB → `MS04` (HTTP 500).

**Output mỗi item:** `name, category, price, status, image, id` (đủ để render bảng + nút Thêm/Sửa/Xóa/Tìm kiếm).

---

## 6. UC21 — Thêm bàn ăn mới

**Entity gợi ý `Table`:**
```
id, code (unique), name (unique), area (optional), capacity,
status (default: 'available'), qr_code_url (nullable),
created_at, updated_at
```

**API:** `POST /api/admin/tables`

**Business rules:**
- BR06.2: `code` là duy nhất → trùng thì `MS02`.
- BR06.3: `name` bắt buộc, không rỗng → thiếu thì `MS01` (cùng field `code` trống cũng vào MS01).
- Tên bàn trùng → `MS03`.
- BR06.4: `capacity > 0` → không hợp lệ thì `MS04`.
- BR06.5: bàn mới luôn có `status = 'available'` mặc định.
- Lỗi lưu DB → `MS05`. Thành công → `MS06`.

---

## 7. UC22 — Xóa bàn ăn

**API:** `DELETE /api/admin/tables/:id`

**Business rules:**
- BR07.2: kiểm tra bàn tồn tại → không có thì `MS02`.
- BR07.3: không được xóa nếu bàn đang có khách (`status = 'occupied'`) hoặc có đơn hàng chưa hoàn tất → `MS01`.
- BR07.4: yêu cầu xác nhận (dialog, message `MS04`) trước khi gọi API xóa thật.
- Khuyến nghị Soft Delete (BR07.6).
- Lỗi DB → `MS03`. Thành công → `MS05`.

---

## 8. UC23 — Tạo mã QR cho bàn ăn

**API:** `POST /api/admin/tables/:id/qr-code`

**Business rules:**
- BR08.2: mỗi bàn chỉ có **một** mã QR đang hoạt động (ghi đè khi tạo lại).
- BR08.3: nội dung mã QR phải là URL trỏ đến menu điện tử của **đúng bàn đó**, ví dụ: `https://<domain>/menu?table_id={id}&token={hash}` (khuyến nghị dùng token/hash thay vì id thuần để tránh đoán URL bàn khác).
- Dùng thư viện tạo QR phía backend (ví dụ `qrcode` cho Node.js, `ZXing`/`zxing-core` cho Java, `endroid/qr-code` cho PHP...).
- Lưu ảnh QR (file hoặc base64) + cập nhật `qr_code_url` trong bảng `tables`.

**Luồng xử lý:**
1. Kiểm tra bàn tồn tại → không có thì `MS01`.
2. Nếu bàn đã có QR → hỏi xác nhận tạo lại (`MS04`), nếu đồng ý thì ghi đè.
3. Sinh URL/định danh bàn → generate QR image → lưu DB/storage.
   - Lỗi khi sinh mã → `MS02`.
   - Lỗi khi lưu → `MS03`.
4. Trả về ảnh QR (base64 hoặc URL) để hiển thị + cho phép tải xuống (`MS06`) / in.

---

## 9. UC24 — Thống kê doanh thu

**API:** `GET /api/admin/revenue-stats?type=day|week|month|year|custom&from=&to=`

**Business rules:**
- BR09.2: chỉ tính đơn hàng có `payment_status = 'paid'` (loại trừ đơn hủy/chưa thanh toán).
- BR09.3: hỗ trợ các mốc thời gian: ngày/tuần/tháng/năm hoặc khoảng tùy chọn (`from`, `to`).
- BR09.5: validate `from <= to`, nếu không → `MS02`.
- BR09.4: trả dữ liệu dạng phù hợp để vẽ biểu đồ (line/bar chart theo thời gian) + bảng tổng hợp (tổng doanh thu, số đơn hàng, giá trị đơn trung bình...).

**Response gợi ý:**
```json
{
  "success": true,
  "summary": { "totalRevenue": 0, "totalOrders": 0, "avgOrderValue": 0 },
  "chartData": [ { "period": "2026-07-01", "revenue": 0, "orders": 0 } ]
}
```

**Các trường hợp đặc biệt:**
- Không có dữ liệu trong khoảng chọn → `MS01` (không phải lỗi, chỉ là empty state).
- Khoảng thời gian không hợp lệ → `MS02`.
- Lỗi truy vấn DB → `MS03` (HTTP 500).

---

## 10. UC25 — Tạo sự kiện giảm giá theo % sản phẩm

**Entity gợi ý `Promotion`:**
```
id, name, discount_percent, start_date, end_date,
dish_ids (many-to-many qua bảng promotion_dishes),
status (upcoming/active/ended), created_at
```

**API:** `POST /api/admin/promotions`

**Business rules:**
- BR10.1/thiếu field: `name` rỗng hoặc chưa chọn sản phẩm nào → `MS01`.
- BR10.2: `discount_percent` phải trong khoảng `1–100` → sai thì `MS02`.
- BR10.3: `start_date <= end_date` → sai thì `MS03`.
- BR10.4: **quan trọng** — mỗi sản phẩm chỉ được thuộc **một** chương trình khuyến mãi còn hiệu lực tại cùng thời điểm. Cần kiểm tra overlap thời gian với các promotion khác đang chứa cùng `dish_id`:
  ```sql
  SELECT * FROM promotion_dishes pd
  JOIN promotions p ON p.id = pd.promotion_id
  WHERE pd.dish_id IN (:selectedDishIds)
    AND p.status != 'ended'
    AND (:start_date <= p.end_date AND :end_date >= p.start_date)
  ```
  Nếu có kết quả trùng → `MS04`.
- BR10.5: chỉ cho chọn sản phẩm đang kinh doanh (`dish.status = 'active'`).
- BR10.6: hệ thống tự động kích hoạt/kết thúc khuyến mãi theo thời gian — dùng **cron job / scheduled task** (chạy mỗi giờ hoặc mỗi ngày) để:
  - Cập nhật `status = 'active'` khi `now >= start_date`.
  - Cập nhật `status = 'ended'` và khôi phục giá gốc khi `now > end_date`.
  - Khi tính giá hiển thị cho khách: `finalPrice = dish.price * (1 - discount_percent/100)` nếu có promotion đang `active` áp dụng cho dish đó.
- Lỗi lưu DB → `MS05`. Thành công → `MS06`.

---

## 11. Gợi ý cấu trúc thư mục backend (tham khảo, áp dụng cho stack bất kỳ)

```
/admin
  /categories    -> controller/service/routes cho UC16
  /dishes        -> controller/service/routes cho UC17-20
  /tables        -> controller/service/routes cho UC21-23
  /statistics    -> controller/service cho UC24
  /promotions    -> controller/service/routes + cron job cho UC25
/shared
  /middlewares   -> auth, checkAdminRole, uploadValidator
  /utils         -> qrGenerator, imageUpload, dateRangeValidator
```

## 12. Việc cần làm khi bắt đầu code

1. Thiết kế/migrate schema DB cho `categories, dishes, tables, promotions, promotion_dishes` (thêm cột soft-delete).
2. Viết middleware xác thực + phân quyền Admin dùng chung cho toàn bộ route `/admin/*`.
3. Code UC16 (danh mục) **trước tiên** vì UC17 phụ thuộc vào việc đã có ít nhất một danh mục.
4. Viết từng API còn lại theo đúng bảng message lỗi (MS0x) ở trên — nên tạo 1 file constants `messages.js` (hoặc tương đương) map `code -> nội dung tiếng Việt` để tái sử dụng và dễ đổi ngôn ngữ sau này.
4. Viết unit test cho các business rule quan trọng nhất: trùng tên món trong danh mục (BR02.6/BR03.4), không xóa được món/bàn đang dùng (BR04.3/BR07.3), overlap khuyến mãi (BR10.4).
5. Viết cron job xử lý bật/tắt khuyến mãi tự động (BR10.6).
6. Frontend: form thêm/sửa món ăn, danh sách bàn + nút tạo/tải QR, dashboard biểu đồ doanh thu (line/bar chart), form tạo khuyến mãi có multi-select sản phẩm.

---

*Ghi chú: toàn bộ nội dung message (MS01, MS02...) và tên trường nghiệp vụ giữ nguyên tiếng Việt theo đặc tả gốc để khớp với UI hiện có. Nếu cần đổi sang tiếng Anh, chỉ cần sửa file constants message, không ảnh hưởng logic.*
