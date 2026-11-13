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

## 13/11/2026 — G1: BỎ NGÀY → CHẠY LIÊN TỤC (đã duyệt G0, luật mới theo chỉ thị chủ dự án)

Thay SRS v3 mục liên quan "ngày/ván 8 ngày/Uy tín 85+". Tag trước thay đổi: `day-based-final` (77f8bf5).

1. **Đồng hồ ván**: `GameState.ElapsedSeconds` (float giây mô phỏng). `Simulation.Advance(world, state, dt)` thuần, xác định theo seed, không đọc thời gian hệ thống. UI cộng dồn dt thật rồi gọi Advance theo bước cố định `GameConfig.simTickSeconds` (mặc định 0.1s, clamp số tick/frame — làm ở G2). `BalanceProbe` dùng đúng `simTickSeconds`.
2. **Tốc độ**: `GameConfig.secondsPerRouteUnit = 20` (giây/route-unit ở tốc Thường); `SpeedUnitsPerSecond(speed) = SpeedValue(speed) / secondsPerRouteUnit` (Fast 1.5×, Normal 1.0×, Slow 0.7×).
3. **Đơn hàng**: `OrderDef.deadlineDay` → `deadlineSeconds` (giây đếm từ lúc đơn xuất hiện; `OrderState.DeadlineAtSeconds = lúc bốc + deadlineSeconds`). Spawn: O-01/O-02 tutorial lúc 0 giây; sau đó 1 đơn mỗi `orderIntervalSeconds = 40` rút ngắn ×`orderIntervalDecayPerOrder = 0.92` theo số đơn đã bốc, sàn `orderIntervalMinSeconds = 15`. Hộp thư quá hạn → xóa, KHÔNG tính trễ. `maxActiveOrders = 4` giữ nguyên.
4. **Trễ**: đơn Accepted/Assigned quá hạn → `IsLate` chốt MỘT LẦN ngay lúc quá hạn, `LateCount++` (không đợi lúc giao). Giao muộn sau đó chỉ doanh thu ×`lateRevenueFactor = 0.5`. **Reputation ĐÃ BỎ HOÀN TOÀN** (chỉnh 4 của chủ dự án 13/11): không còn field, không phạt, không hiển thị — chỉ còn doanh thu là thước đo phụ.
5. **Thắng/thua**: thắng khi `DeliveredCount >= winDeliveries = 10`; thua khi `LateCount >= maxLateDeliveries = 3`. **Reputation KHÔNG còn điều kiện kết thúc** — đã bỏ hẳn (mục 4).
6. **Kết cục** (`EndingEvaluator.FromOutcome`): thắng 0 trễ → E1, 1 trễ → E2, ≥2 trễ → E3; thua → E4 (đã giao ≥ `earlyLoseDeliveries = 2`) hoặc E5 (đã giao < 2). Văn bản 5 kết cục cập nhật trong Endings.asset theo luật mới (bỏ "Uy tín 85+").
7. **Sự cố**: roll khi tàu vừa VÀO cạnh mới (kể cả tick xuất phát — cạnh 0), qua `IncidentScheduler.TryTrigger(world, state, ctx)`, xác suất `incidentChancePerEdge = 0.15`, tối đa `maxPendingIncidents = 2` cùng lúc; trigger lấy hazard của CẠNH HIỆN TẠI (không cả tuyến). I-01 = bắt buộc lần ĐẦU TIÊN một tàu vào cạnh khớp trigger (Sương mù); I-F = một lần khi `DeliveredCount == winDeliveries - 1` và có tàu chở hàng. Sự cố chờ → đồng hồ DỪNG (`CanAdvance == false`), UI xử lý xong mới chạy tiếp. `incidentsWhileRoaming = false` (chừa cho [S] roaming).
8. **BalanceProbe**: mô phỏng liên tục bằng Advance(simTickSeconds), 2 chính sách ngẫu nhiên/đọc-manh-mối; `SimulateRuns` trả tỉ lệ thắng, `SimulateRunsMeasured` đo thêm thời lượng ván trung bình; chặn kẹt ở 1800s mô phỏng (NFR-06). 400 ván nằm ở menu `Tools/AyeAye/Balance`; test thường chạy số ván nhỏ.
9. **GameConfig mất field**: `gameLengthDays`, `newOrdersPerDayMin/Max`, `maxIncidentsPerDay`, `incidentChancePerMovingShip`, ngưỡng kết cục theo Uy tín, `deadlineDay`. Asset cũ thiếu key mới → dùng giá trị mặc định ghi ngay trong class (không regenerate asset).
10. **Deadline O-01..O-08** đặt theo kiểm tra DemoValidator "hạn ≥ ETA tàu nhanh nhất từ cảng lấy hàng + `deadlineReactionMarginSeconds` (25 s) + `tutorialDeadlineBonusSeconds` (30 s cho O-01/O-02)" (điều 6 duyệt G0 + chỉnh 3 phản hồi đánh giá G1): O-01 = 70s, O-02 = 85s, O-03 = 75s, O-04 = 45s, O-05 = 45s, O-06 = 90s, O-07 = 90s, O-08 = 75s. Thay đổi số cân bằng chỉ được qua GameConfig/DemoData, không hard-code ngoài data.
11. **StagedCommands bỏ hoàn toàn** (điều 4 duyệt G0): MAKE IT SO điều tàu NGAY, không hủy được; pha Planning/XÁC NHẬN/HỦY LỆNH và `GamePhase` xóa. Pha UI chỉ còn Chạy / Sự cố chờ (`PhaseGate.GetAccess`).

## 13/11/2026 — G2: UI theo phản hồi đánh giá G1

1. **Màn BẮT ĐẦU quay lại**: máy bật → màn "BẮT ĐẦU" với 3 dòng hướng dẫn (nhận đơn/điều tàu, thắng-thua, sự cố chờ dừng đồng hồ); đồng hồ chỉ chạy sau khi bấm BẮT ĐẦU. (Ghi đè quyết định "bỏ màn start" ngày 12/11 — reply chủ dự án 13/11 yêu cầu lại.)
2. **Pause mất focus**: `Application.focusChanged` + `OnApplicationFocus/OnApplicationPause` → dừng cộng dt; máy tắt (Esc) cũng không chạy đồng hồ. Quay lại: dt clamp 0.5 s nên không nhảy giờ.
3. **Thanh trên**: "Thời gian mm:ss · Trễ x/3 · Đã giao n/N · Doanh thu" (thêm nhãn `top-delivered`).
4. **Hạn đơn ~10 Hz**: label hạn đơn sửa text TẠI CHỖ (registry (đơn, label, tiền tố, hậu tố) dựng lại mỗi Refresh) — không dựng lại panel, giữ vị trí cuộn; chấm tàu vẫn mỗi frame.
5. **Tab SỰ CỐ có UI resolve** (thiếu trước đó — ván không thể kết thúc khi có sự cố chờ): thẻ sự cố + nút theo phương án → `IncidentResolver.Resolve/Apply` + `Simulation.ApplyProgressDelta` → hiện dòng kết quả cho đến sự cố kế tiếp. Đồng hồ dừng khi còn sự cố chờ (`CanAdvance == false`).
6. **PlayCapture**: driver tự chơi (sự cố phương án đầu, nhận đơn còn về kịp hạn, điều tàu tuyến nhanh nhất) + chụp mốc: start-gate, tab ĐƠN HÀNG, tab ĐỘI TÀU, tab SỰ CỐ, màn Ending — chạy batch được bằng `-executeMethod PlayCapture.BuildValidateCapture`.

