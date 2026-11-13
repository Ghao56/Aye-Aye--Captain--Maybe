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

## 13/11/2026 — Tab ĐỘI TÀU (G3)

1. **Tọa độ bản đồ**: thêm `PortDef.mapPosition` (Vector2 chuẩn hóa 0..1, sửa được trong Inspector) — dữ liệu vị trí nằm trong asset Port_* do Build Demo dựng, C# không hard-code tọa độ.
2. **Quy tắc phân đoạn**: `RouteProgress` (hàm thuần dùng chung) tái lập đúng công thức cũ `length × nodeIndex / edges` của SimulationStep (cạnh dài đều); SimulationStep giờ gọi hàm này — hành vi mô phỏng KHÔNG đổi.
3. **Animation tàu sau MAKE IT SO**: chỉ ở UI (snapshot trước khi chạy Simulate, chấm chạy trên tuyến CŨ của state trước đó); không đổi state/Rng → cùng seed cùng kết quả. Thời lượng `config.fleetAnimMs` (GameConfig, mặc định 1200 ms).
4. **Pha Incident**: hiện sự cố tương tác chưa có (Bước 5) → sau MAKE IT SO chỉ chạy animation tàu rồi vào Tổng kết; khi Bước 5 có UI sự cố thì sự cố hiện SAU animation.
5. **Skip animation**: bấm chuột vào bản đồ → nhảy tới vị trí cuối ngay.
6. **Màu cạnh theo hazard** được vẽ bằng Painter2D nên lấy từ hằng C# (`HazardColor`) khớp biến màu chú giải trong Terminal.uss (`--c-h-*`) — đổi màu phải sửa cả hai nơi.
7. **Điều tàu rỗng**: một cặp cảng có thể có 2 RouteOption → hiện một nút cho từng tuyến; bấm nút là staged luôn (có preview + ETA trên thẻ tàu) — bấm nhầm thì dùng HỦY LỆNH.
8. **Chú giải hazard** hiển thị tên enum tiếng Anh (Clear/Fog/Storm/Pirates/Ice) — nhãn tiếng Việt nếu chủ dự án muốn thì sửa sau.
