# ASSUMPTIONS.md — Giả định & sai khác với SRS

## 13/11/2026 — Bản đồ hình sao (thay SRS §5.1–5.2)

Chủ dự án yêu cầu đổi bản đồ 6 cảng / 7 cạnh (SRS §5) thành hình sao 5 nút.
SRS.md v2 KHÔNG được sửa — sai khác ghi tại đây.

### Bố cục mới
- `goc` = Trạm Trung Chuyển (isHeadquarters = true), vẽ hình vuông ở giữa bản đồ.
- 4 hành tinh: `vang`, `suong`, `camap`, `tram` (tên hiển thị giữ nguyên trừ `goc` đổi từ "Cảng Gốc" → "Trạm Trung Chuyển").
- 6 cạnh: 4 nan hoa goc–vang (1, Clear), goc–suong (1, Fog), goc–camap (2, Pirates), goc–tram (2, Ice) + 2 cạnh phụ suong–camap (1, Fog), camap–tram (1, Storm).
- Đã bỏ `mogio` (Mỏm Gió) và mọi tham chiếu; DemoBuilder giờ xóa asset mồ côi khi chạy Build Demo.

### Ảnh hưởng & lý do
1. Cạnh phụ là **suong–camap (Fog)** chứ không phải "vang–suong (Storm)" như đề xuất ban đầu: O-02 (Goc→Camap, giữ nguyên from/to theo tinh thần tutorial) PHẢI có tuyến 2 chặng đi qua cạnh Fog để I-01 rơi ngày 1 (FR-0502). Không có suong–camap thì O-02 chỉ còn tuyến Pirates thẳng — mất tutorial.
2. Storm vẫn xuất hiện ở cạnh camap–tram → I-02 (Bão) còn kích hoạt được.
3. O-03 đổi Goc→Mogio thành Goc→Tram; O-08 đổi Mogio→Camap thành Suong→Tram (mogio bị bỏ).
4. Mỗi cặp cảng có tối đa 2 RouteOption; 10 cặp đều có tuyến (test chốt).
5. Số route-units của vài tuyến dài hơn trước (vd Vang→Tram = 3 thay vì 3; Suong→Camap = 1 như cũ) — cân bằng qua BalanceProbe, KHÔNG đổi GameConfig.
