# SDD ledger — plan: G5 (prompt của chủ dự án 14/11, tàu roaming có mục đích)

Quy ước: prompt G5 của chủ dự án là spec; quyết định "chỉ giao việc khi cập cảng" đã chốt.
Chạy test/capture trên sandbox mirror `C:\Users\Administrator\AppData\Local\Temp\opencode\aa-mirror`
(Unity GUI đang giữ project chính D:\Aye-Aye--Captain--Maybe, PID Unity nhiều process).

- TDD RED: viết `RoamTests.cs` (11 test) + `DemoValidatorTests.RoamProfileInvalid_Reported` (1 test)
  TRƯỚC khi có logic. Đang chạy lượt đỏ trên mirror (bộ lọc RoamTests+DemoValidatorTests).
  Ruling: scaffolding dữ liệu (RoamProfile/config/ShipDef.roam/ShipState.Roam/RoamRng/HQPortId/
  InitializeShips copy) viết trước để test compile — coi là mặt API, không phải hành vi roaming;
  lượt đỏ phải FAIL về HÀNH VI (tàu không rời cảng, validator chưa có nhánh Roaming, validator
  demo chưa kiểm dwell), không phải lỗi compile.
- RED (11:18): RoamTests+DemoValidatorTests = 17 test, 8 FAIL đúng lý do thiếu hành vi:
  7× "Expected: Roaming"/"tiền điều kiện" (tàu chưa bao giờ rời Idle) + 1× dwellSeconds (validator
  demo chưa kiểm hồ sơ). 4 test assert-sự-vắng-mặt xanh tạm (không-dư-thừa, không-roam, Rng-isolation,
  cùng-seed) — sẽ có ý nghĩa khi logic sống. Kết quả: C:\...\Temp\opencode\aa-red.xml
- GREEN (lần 2 sau 2 lỗi compile: cast float RoamPlanner.NextDouble, Label.text Fleet.cs):
  RoamTests+DemoValidatorTests = 17/17 PASSED. Kết quả: C:\...\Temp\opencode\aa-green.xml
- FULL EditMode (lần 2, sau khi thêm DemoDataShips_CoHoSoRoam): 109/109 PASSED
  (96 cũ + 12 RoamTests + 1 DemoValidatorTests = 109; không test cũ nào bị sửa hay cần roamEnabled=false).
- DoD grep Rng: 0 dùng state.Rng trong RoamPlanner.cs (chỉ dòng comment) + Simulation.cs (0 match).
  USS: không thêm rgb/rgba mới — chỉ `.map-dot.roaming { opacity: 0.6; }`.
- Balance lượt 1 (asset CŨ mặc định do lỗi quên gán s.roam trong DemoData.BuildShips — đã sửa +
  thêm test DemoDataShips_CoHoSoRoam): NGẪU NHIÊN 43% (171/400) [E1 14 · E2 57 · E3 100 · E4 229 ·
  E5 0], TB 299s, p10/50/90 227/306/356s, trễ TB 2.36, kẹt 0; ĐỌC MANH MỐI 93% (370/400)
  [E1 135 · E2 150 · E3 85 · E4 30 · E5 0], TB 331s, p10/50/90 295/328/379s, trễ TB 1.03, kẹt 0.
- BALANCE CHÍNH THỨC (400 ván × 2 chính sách, asset hồ sơ đúng, seedBase 1000):
  NGẪU NHIÊN: thắng 34% (135/400) [E1 5 · E2 29 · E3 101 · E4 265 · E5 0], TB 284s,
    p10/50/90 207/286/356s, trễ TB 2.57 đơn, kẹt 0/400.
  ĐỌC MANH MỐI: thắng 79% (316/400) [E1 55 · E2 167 · E3 94 · E4 84 · E5 0], TB 324s,
    p10/50/90 272/328/374s, trễ TB 1.52 đơn, kẹt 0/400.
  → 96% cũ KHÔNG còn hiệu lực: tàu rời cảng tự do làm bot đọc-manh-mối tụt 96%→79%.
    Núm đề xuất chủ dự án: dwellSeconds (nghỉ ngắn hơn), roamSpeedFactor, wanderWeight,
    roamStartSeconds (kéo dài ân hạn không ảnh hưởng giữa ván). KHÔNG tự chỉnh.
- CAPTURE (batchmode KHÔNG -nographics, mirror): tới Ending E3_Survived — giao 10, trễ 2,
  doanh thu 995, 365s mô phỏng. Shots g5-* ở C:/Users/Admin/AppData/Local/Temp/opencode/aa-shots:
  tab-orders, tab-fleet (tàu chọn đảo màu), tab-incidents, incident-active, fleet-roaming
  (tàu Roaming giữa cạnh, chấm mờ 0.6 ≠ tàu Idle sáng), orders-warn ("! " nhấp nháy), ending.
  graycheck.py: 13 ảnh kênh-xanh — tàu Roaming/Idle phân biệt bằng độ sáng, legend giữ ký tự
  mẫu nét (─ ··· ━ ━ ─·─ ∿). Lưu ý: "tới sau Ns" trên thẻ có thể bị cắt vì tên cảng dài (trạm
  "Trạm Trung Chuyển") — cơ chế label 10 Hz đúng, chi tiết hiển thị chờ review chủ dự án.
- BuildDemo lần 3: asset đúng hồ sơ (Bob dwell 8/wander 2; Chen 30/1; Mira 15/0; Dex 15/6).
- Bẫy thêm: batch Unity trên mirror thi thoảng KHÔNG tự thoát sau executeMethod → instance kế
  bị "another Unity instance running" → kill PID Unity-trên-mirror trước mỗi lượt chạy mới.
  Capture bị frame xám trống khi chạy với -nographics → G5 capture chạy KHÔNG -nographics
  (ledger event-pool: capture batchmode chỉ chạy không -nographics).
