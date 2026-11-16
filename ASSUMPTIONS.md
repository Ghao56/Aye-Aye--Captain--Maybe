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

1. ~~**Màn BẮT ĐẦU quay lại**~~ — **BỔ lại 14/11: chủ dự án yêu cầu BỎ màn BẮT ĐẦU**. Máy bật → ván chạy ngay (`Update` tự `StartNewRun` khi `state == null`); nút `start-button` xóa khỏi UXML, `ShowStartGate`/`atStartGate` xóa khỏi HUD. Màn `start-screen` chỉ còn dùng cho **màn kết thúc** (title + nút CHƠI LẠI).
2. **Pause mất focus**: `Application.focusChanged` + `OnApplicationFocus/OnApplicationPause` → dừng cộng dt; máy tắt (Esc) cũng không chạy đồng hồ. Quay lại: dt clamp 0.5 s nên không nhảy giờ.
3. **Thanh trên**: "Thời gian mm:ss · Trễ x/3 · Đã giao n/N · Doanh thu" (thêm nhãn `top-delivered`).
4. **Hạn đơn ~10 Hz**: label hạn đơn sửa text TẠI CHỖ (registry (đơn, label, tiền tố, hậu tố) dựng lại mỗi Refresh) — không dựng lại panel, giữ vị trí cuộn; chấm tàu vẫn mỗi frame.
5. **Tab SỰ CỐ có UI resolve** (thiếu trước đó — ván không thể kết thúc khi có sự cố chờ): thẻ sự cố + nút theo phương án → `IncidentResolver.Resolve/Apply` + `Simulation.ApplyProgressDelta` → hiện dòng kết quả cho đến sự cố kế tiếp. Đồng hồ dừng khi còn sự cố chờ (`CanAdvance == false`).
6. **PlayCapture**: driver tự chơi (sự cố phương án đầu, nhận đơn còn về kịp hạn, điều tàu tuyến nhanh nhất + reposition tàu rỗi) + chụp mốc: tab ĐƠN HÀNG, tab ĐỘI TÀU, tab SỰ CỐ, sự cố đang chờ, màn Ending — chạy batch được bằng `-executeMethod PlayCapture.BuildValidateCapture`.

## 14/11/2026 — Review báo cáo G2 (chủ dự án)

1. **Hướng dẫn cách chơi vào tab ĐƠN HÀNG** (thay màn BẮT ĐẦU đã bỏ): dòng `Clue` đầu tab, dùng số từ config (winDeliveries/maxLateDeliveries), tự ẩn sau lần NHẬN đầu tiên (`activeCount == 0` mới hiện).
2. **Balance: GIỮ 96%, không siết ngay** — bot đọc-manh-mối là cận trên (phản ứng tức thì, biết chính xác ETA); người thật thấp hơn. Chờ 2–3 người chơi thật playtest rồi mới chỉnh. Nếu siết: thử `incidentChancePerEdge` (0.15 → 0.25) trước vì không đụng validator; **lưu ý `deadlineReactionMarginSeconds` chỉ được DemoValidator dùng để kiểm tra biên, KHÔNG ảnh hưởng gameplay** — siết thật phải đổi `deadlineSeconds` từng đơn trong DemoData / `incidentChancePerEdge` / `orderIntervalSeconds` / `maxLateDeliveries`.
3. **Xóa GameStateTests (PlayMode)** — 7 test đều rỗng (`IsNotNull`) hoặc trùng coverage EditMode; giữ TerminalInputTests (test click máy thật).
4. **AdvanceWithFixedTimestep** chuyển thành vòng lặp gọi `Advance` (không còn 2 bản logic trùng).
5. **Đối chiếu số test 114 → 96 (chính xác)**: b176241 xóa 3 file = −20 attr (DayRunner 6, GameStateFlow 4, SimulationStep 10); file còn lại: EndingEvaluator −4, PhaseGate −3, ScoreCalculator −1, DemoValidator +2, OrderScheduler +1 (net −5) → 89; SimulationTests mới +7 → **96** (khớp XML). Bảng cũ ghi "xóa 20, thêm 7 → 101" thiếu net −5 của các file còn lại.


## 14/11/2026 — G5: tàu roaming có mục đích

1. **Roaming là thật trong `Simulation`** (không còn là hiệu ứng UI): tàu `Idle` có `Roam != null`, qua ân hạn `roamStartSeconds` (30s — người chơi mới đọc hướng dẫn), hết nghỉ (`dwellSeconds`) và là tàu DƯ THỪA tại cảng thì rời đi 1 cạnh TRỰC TIẾP tới cảng kề (RoamPlanner.ChooseNext). Đi theo tốc × `roamSpeedFactor` (0.7), tới đích về `Idle`, đặt lại timer = dwell. Không roll sự cố khi roaming (`incidentsWhileRoaming = false`), không chở hàng.
2. **Quy tắc "tàu dư thừa mới rời cảng"**: số tàu `Idle` tại cảng > số đơn InInbox + Accepted-chưa-gán có FromPortId = cảng đó. Đơn tutorial sinh tự động lúc 0s trong Advance nên tàu thừa đầu ván vẫn đi được (sau ân hạn 30s).
3. **Chỉ giao việc khi cập cảng**: tàu `Roaming` bị từ chối Assign/Reposition với lý do nêu đích đến + số giây còn lại. Giữ tên enum `Idle` (= đậu ở cảng), không thêm `Docked`.
4. **`RoamRng` riêng** (`seed ^ 0x5F5F5F5F`): cấm dùng `state.Rng` trong code roaming — test `RoamKhongDungRngChung_ChoiDonGiongHet` chứng minh bằng thực nghiệm (chuỗi đơn sinh ra giống hệt khi roam bật/tắt). Người chơi không điều khiển tàu roaming; tàu roaming không mang đơn.
5. **Hồ sơ tính cách** (`RoamProfile` trên ShipDef): Bob nghỉ ngắn, thích cảng xa; Chen nghỉ lâu, thích cảng gần + gắn HQ; Mira gắn HQ, không nhiễu (đi đều); Dex nhiễu cao (hay đổi ý). Giá trị KHỞI ĐIỂM — đo lại qua Balance, chưa chốt.
6. **UI**: chấm tàu Roaming bám `AlongRoute` theo state, opacity 0.6 (đơn sắc, không màu mới); thẻ tàu hiện "Đang đi A → B, tới sau Ns" cập nhật 10 Hz tại chỗ (shipEtaLabels); khóa làm mới panel đổi khi trạng thái bất kỳ tàu đổi (rời/cập cảng). RoamTick hiệu ứng UI đã XÓA.
7. **Balance cũ (96%) KHÔNG còn hiệu lực**: tàu rời cảng tự do thay đổi kết quả. Số liệu mới đo bằng `DemoBuilder.Balance` 400 ván/chính sách — xem báo cáo G5. Núm đề xuất: `dwellSeconds`, `roamSpeedFactor`, `wanderWeight`, `backtrackPenalty`.
8. **BalanceProbe/PlayCapture chính sách đọc-manh-mối** ước tính cả tàu Roaming sắp cập cảng: (edge còn lại + FastestSeconds(đích → cảng lấy hàng) + haul) ≤ hạn đơn; gán vẫn chỉ cho tàu Idle.

## 14/11/2026 — G6: tab ĐỘI TÀU thành dashboard 3 vùng (workflow chọn đôi)

1. **Layout mới**: `.panel-fleet` thành cột dọc — hàng trên (bản đồ ~54% + lưới thẻ tàu 2×2 ~46%), hàng "ĐƠN CHỜ ĐIỀU TÀU" rộng toàn đáy (thẻ ngang, tối đa maxActiveOrders), thanh lệnh cố định dưới cùng. Bỏ ScrollView cột phải — tab ĐỘI TÀU không cuộn. Chỉ sửa `TerminalHud.Fleet.cs` + `Terminal.uss`, không đụng logic game.
2. **Chọn đôi, không staged**: bấm thẻ tàu/chấm tàu → chọn tàu; bấm thẻ đơn đã nhận chưa gán → chọn đơn; đơn có >1 tuyến thì nút tuyến là CHỌN tuyến (tô sáng trên bản đồ), GIAO LỆNH mới áp qua `FleetActions.Assign` — hiệu lực NGAY như cũ. Lựa chọn chỉ là state UI (selectedShipId/selectedOrderId/selectedRouteIdx/selectedReposPortId).
3. **GIAO LỆNH vô hiệu kèm lý do**: gọi thẳng `CommandValidator.ValidateAssign` (không tạo hệ thống kiểm tra thứ hai); sau áp thành công bỏ chọn đơn + hiện dòng phản hồi. Tàu roaming/moving: nút vẫn hiện nhưng vô hiệu + đếm giây cập cảng (shipEtaLabels).
4. **Tương thích tàu↔đơn tô đơn sắc**: đơn đang chọn → tàu đậu đúng cảng đi viền sáng, còn lại mờ 45%; tàu đang chọn → đơn khớp cảng đi viền sáng, không khớp mờ. Vẫn bấm được thẻ mờ để đọc lý do từ chối.
5. **Bản đồ**: tuyến của đơn đang chọn vẽ nét `EdgeColorSel` sáng dày hơn; bấm nút hành tinh khi đang chọn tàu đậu → chế độ ĐIỀU TÀU RỖNG với cảng đích đã chọn (nút tuyến khởi hành ngay — hành vi cũ giữ nguyên); nút "TẤT CẢ CẢNG" bỏ lọc đích.

## 15/11/2026 — G6.2: tab ĐỘI TÀU 2 cột (sửa chồng lấn G6)

1. **Nguyên nhân gốc**: kính CRT thật chỉ ~1207×679 px thiết kế ở 16:9 — Terminal GO `m_LocalScale 0.22` + `zoomOutFactor 1.27`, `TerminalView.FitScreenToFrame` đặt `.screen` đúng bằng khung camera. Mọi chữ hiển thị ~1.6× số USS (font 26 → ~41-47 px trên Game view). Bố cục G6 3 khối dọc tính trên ~1080px chiều cao nên thiếu chỗ → flex co `fleet-top`, nội dung overflow đè lên khối kế tiếp (ảnh bug 14/11).
2. **Bố cục G6.2** (`TerminalHud.Fleet.cs` + `Terminal.uss`): 2 cột trong `.fleet-row`. Trái = bản đồ (`min-height: 300px`) + khối "ĐƠN CHỜ ĐIỀU TÀU" dưới bản đồ — thẻ đơn 1 dòng (id · khách | tuyến ngắn | hạn), tối đa ~2 thẻ trong khung 80px, dư thì cuộn (`fleet-orders-scroll`, CRTScroll). Phải = MỘT ScrollView (`fleet-side-scroll`) chứa tiêu đề ĐỘI TÀU + 4 thẻ tàu xếp 1 cột + thanh lệnh (`.fleet-cmd-row` flex-wrap: hẹp thì các nhóm xuống dòng, không chồng).
3. **Chống chồng tuyệt đối**: `.fleet-row/.fleet-left/.fleet-side` đều `overflow: hidden` — vùng nào hết chỗ thì CUỘN trong vùng của mình, không bao giờ vẽ đè lên vùng khác. Không margin âm, không position:absolute cho nội dung thường.
4. **Scroll giữ vị trí qua Refresh**: `SavedScroll(panel, class)` đọc trước `panel.Clear()`, khôi phục bằng `schedule.Execute(...).ExecuteLater(1)` (mẫu của BuildOrdersPanel).
5. **Chip trong thẻ đơn bỏ** (không vừa dòng 1) — tag hàng chuyển vào tooltip thẻ cùng tên cảng đầy đủ; dữ liệu order không đổi.
6. **Chú giải hazard vẫn theo G6.1**: bỏ hẳn legend; hazard chỉ hiện trong tóm tắt lệnh khi tuyến đang chọn có cạnh không Clear (`CẢNH BÁO: FOG…`), tuyến Clear không hiện gì.
7. **Font không dưới 20px** toàn bộ fleet section; mọi số đo nằm trong Terminal.uss; chỉ 4 màu phosphor.
8. **Kiểm chứng Play mode**: `FleetClickCheck.Run` (batch) bấm cả 4 thẻ tàu bằng ClickEvent, kiểm tra picking bị che, đo overlap map/orders/cmd/thẻ, kiểm tra map ≥300px, chụp ảnh đúng góc nhìn kính (camera theo ComputeZoomTarget). EditMode + PlayMode TerminalInputTests chạy lại trên bản copy.

## 15/11/2026 â€” G6.3: tá»‘i giáº£n giao Ä‘Æ¡n + ETA sá»‘ng cho tÃ u Moving

1. **NguyÃªn nhÃ¢n gá»‘c lá»—i 1 (xÃ¡c nháº­n giáº£ thuyáº¿t Ä‘á» xuáº¥t)**: dÃ²ng vá»‹ trÃ­ tÃ u Moving Ä‘Æ°á»£c dá»±ng thÃ nh chuá»—i tÄ©nh trong LocationLine (BuildShipCards) vÃ  chá»‰ dá»±ng láº¡i khi lastRefreshKey Ä‘á»•i; lastRefreshKey cá»‘ tÃ¬nh KHÃ”NG chá»©a DistanceAlongRoute (sáº½ dá»±ng láº¡i panel má»—i frame â†’ máº¥t cuá»™n, giáº­t). Chá»‰ tÃ u Roaming cÃ³ shipEtaLabels (10 Hz). Fix: tÃ u Moving cÅ©ng Ä‘Äƒng kÃ½ nhÃ£n vÃ o shipEtaLabels (má»Ÿ rá»™ng tuple, khÃ´ng danh sÃ¡ch má»›i), UpdateCountdowns tÃ­nh cÃ²n Ns = (Route.length âˆ’ DistanceAlongRoute) / SpeedUnitsPerSecond(ship.Speed); Æ°u tiÃªn "cÃ²n Ns" hÆ¡n %; bá» thanh tiáº¿n Ä‘á»™ kÃ½ tá»± (rá»§i ro glyph font, khÃ´ng báº¯t buá»™c).
2. **Viá»‡c 1b â€” háº¿t trÃ n chá»¯**: hÃ ng nÃºt cháº¿ Ä‘á»™ + nhÃ£n TÃ€U/ÄÆ N Ä‘Ã£ bá»‹ XOÃ háº³n (khÃ´ng cÃ²n cáº§n flex-wrap); panel lá»‡nh má»›i chá»‰ cÃ³ 1 dÃ²ng hÆ°á»›ng dáº«n + tá»‘i Ä‘a 2 nÃºt tuyáº¿n + nÃºt phá»¥; Label trong panel white-space normal (Ä‘Ã¨ Label nowrap máº·c Ä‘á»‹nh), min-width: 0; font khÃ´ng dÆ°á»›i 20px; cáº¯t bá»›t chá»¯ (nhÃ£n cáº£ng ngáº¯n + tooltip Ä‘áº§y Ä‘á»§) thay vÃ¬ thu nhá».
3. **Viá»‡c 2 â€” giao Ä‘Æ¡n â‰¤3 báº¥m**: báº¥m Ä‘Æ¡n â†’ tá»± chá»n tÃ u náº¿u Ä‘Ãºng 1 tÃ u Ä‘á»§ Ä‘iá»u kiá»‡n (Idle + Ä‘Ãºng cáº£ng Ä‘i, trong SelectOrder) â†’ báº¥m nÃºt tuyáº¿n = FleetActions.Assign NGAY (bá» nÃºt GIAO Lá»†NH riÃªng + bá» selectedRouteIdx/SelectedEdgeSet/EdgeKey/EdgeColorSel + bá» highlight tuyáº¿n trÃªn báº£n Ä‘á»“). NhÃ£n nÃºt: length nhá» nháº¥t = NHANH, cÃ²n láº¡i = AN TOÃ€N, hai tuyáº¿n báº±ng nhau â†’ route.label; hazard contextual "! SÆ°Æ¡ng mÃ¹/BÃ£o/CÆ°á»›p/BÄƒng" (HazardShort). ÄÆ¡n Ä‘Ã£ giao â†’ reset cáº£ selectedOrderId LáºªN selectedShipId.
4. **Tháº» Ä‘Æ¡n 2 dÃ²ng**: KhÃ¡ch â†’ Cáº£ng Ä‘Ã­ch / háº¡n Ns Â· +doanh thu Â· chip [!] [*] [>>] (SymbolOf cáº¯t tÃªn chip, tÃªn Ä‘áº§y Ä‘á»§ á»Ÿ tooltip); .fleet-orders-scroll max-height 124px (2 tháº»), dÆ° thÃ¬ cuá»™n â€” giá»¯ CRTScroll + SavedScroll.
5. **Äiá»u rá»—ng háº¡ cáº¥p**: bá» 2 nÃºt mode lá»›n (btn-mode chá»‰ cÃ²n dÃ¹ng cho Táº¤T Cáº¢ Cáº¢NG trong reposition); Ä‘iá»u rá»—ng = nÃºt phá»¥ .btn-sub "Chuyá»ƒn tÃ u rá»—ng sang cáº£ng khÃ¡c", chá»‰ hiá»‡n khi Ä‘Ã£ chá»n tÃ u Idle; cÃ³ nÃºt â† GIAO ÄÆ N Ä‘á»ƒ thoÃ¡t. TÃ u Moving/Roaming Ä‘Æ°á»£c chá»n: khÃ´ng nÃºt, chá»‰ dÃ²ng ETA cáº­p 10 Hz (AddShipWaitLabel, dÃ¹ng chung shipEtaLabels; xoÃ¡ dÃ²ng tÄ©nh "TÃ u Ä‘ang cháº¡y â€” xem trÃªn báº£n Ä‘á»“").
6. **Cháº¥m tÃ u má» theo Ä‘Æ¡n**: Ä‘Æ¡n Ä‘ang chá»n â†’ dot tÃ u khÃ´ng Ä‘á»§ Ä‘iá»u kiá»‡n thÃªm class .map-dot.dimmed (opacity 0.35) â€” Ä‘á»“ng bá»™ vá»›i tháº» tÃ u compatible/dimmed. KhÃ´ng mÃ u má»›i.
7. **Giá»¯ nguyÃªn má»i tÃªn reflection PlayCapture** (state, selectedShipId, focusPaused, Dirty, SelectTab) vÃ  tÃªn element UXML; FleetClickCheck má»Ÿ rá»™ng: báº­t mÃ¡y báº±ng TerminalView.FinishIntro qua reflection (batch khÃ´ng ai báº¥m mÃ¡y), 2 máº«u ETA cÃ¡ch 1.5s KHÃ”NG báº¥m (kiá»ƒm chá»©ng cháº¡y ná»n), test tá»± chá»n tÃ u khi báº¥m Ä‘Æ¡n, Ä‘áº¿m Label trÃ n cha (xMax > parent xMax + 1).
8. **Kinh nghiá»‡m batch**: Unity.exe lÃ  bootstrapper â€” fork tiáº¿n trÃ¬nh editor tháº­t rá»“i thoÃ¡t ngay â†’ wrapper PowerShell bÃ¡o completed áº£o vá»›i exit code rá»—ng; pháº£i Get-CimInstance tÃ¬m PID cá»§a tiáº¿n trÃ¬nh -batchmode -projectPath rá»“i Wait-Process PID Ä‘Ã³. KHÃ”NG chain 2 lá»‡nh Unity trong 1 shell (thá»© 2 dÃ­nh lock another-Unity-instance). Modal Recovering Scene Backups giáº¿t batch mode â†’ xoÃ¡ Library/Logs/UserSettings cá»§a báº£n copy vÃ  import láº¡i.

## G6.3b - Bug "giao don 2 game dung, UI loi" (14/11/2026)
- NGUYEN NHAN (tai hien bang batch driver FleetAssignRepro, khong phai doan): giao don 2 -> tau thu 2 roi cang -> roll su co I-01 ngay khi vao canh dau -> PendingIncidents>0 -> CanAdvance=false -> dong ho dung (thiet ke G4). UI KHONG tu mo tab SU CO nen nguoi choi o tab DOI TAU chi thay dong ho dung + badge "SU CO (1)" -> tuong game loi. Khong co exception nao.
- FIX (render-only, TerminalHud.cs): field lastPendingIncidents + trong Refresh() khi pending chuyen 0->>0 -> SelectTab(2) tu mo tab SU CO (the su co + nut phuong an hien ngay, de "SU CO DANG CHO - dong ho dung").
- XAC NHAN: repro batch - sau giao don 2 panel-2 bat on; sau khi chon phuong an (ResolvePendingIncident) dong ho chay lai delta 3.0s/3s, pending=0, CanAdvance=true; EditMode 109/109 pass.
- Driver lessons (batch khong co focus): UI Toolkit Button khong an ClickEvent gui tay; selection qua reflection + Dirty(); mo tuong minh Assets/_Project/Scenes/Main.unity trong Run() (Library reset -> scene rong); FinishIntro dung 1 lan; clear focusPaused moi tick.

## G7 - Bug "giao don thu 3 game dung" + sinh don NGAU NHIEN + bo chan tau (16/11/2026)

### 1. Bug "giao don thu 3 thi game dung" - NGUYEN NHAN GOC
- Khong co hang sim: 400/400 seed x 2 policy chay het van, 0/400 ket (BalanceProbe + StuckClockTests sweep seed 1..50). Dong ho dung thiet ke (G4: su co cho) hoac mat focus.
- Van de LA UI/UX: khi CanAdvance=false (su co cho) hoac mat focus, UI chi doi text topbar khi lastRefreshKey doi -> o tab DOI TAU nguoi choi chi thay dong ho dung, khong biet ly do; them ma Refresh() nem exception thi lastRefreshKey da bi ghi tu truoc -> UI dong bang vinh vien (khong tu hoi phuc).
- FIX (render-only, TerminalHud.cs):
  1. lastRefreshKey = key ghi SAU khi Refresh() chay tron -> UI tu hoi phuc sau exception.
  2. UpdateTopPhase() goi MOI frame: TAM DUNG (mat focus) / TAM DUNG (su co cho) / KET THUC / DANG CHAY.
  3. Su co moi pending 0->>0: tu mo tab SU CO + nhay nhap tab (class blink-off tai dung).
  4. ignoreFocusPause (debug editor) bo qua pause mat focus.
  5. Dem nguoc "don dau tien sau ~Ns" trong tab DON HANG (10 Hz) - vi G7 bo don co san.
  6. NewRun reset lastPendingIncidents (van moi khong nhay tab nham).
- Bang chung seed: StuckClockTests quet seed 1..50 x 2 policy - dong ho luon tien khi chua ket + khong con su co cho; bat bien 1500s: khong don nao bi xoa khi dang Accepted/Assigned (bug AddOrder fallback RemoveAll cu).

### 2. Bo chan tau theo thuyen truong/tag (Phan 2)
- CommandValidator.ValidateAssign khong con kiem tra captain/tag (da la hanh vi cu, test moi Assign_KhongChanTheoThuyenTruongHoacTag chot lai). Manh moi chi con la huong dan, khong con canh bao tau.
- O-04 "Dung cho Bob cho" sua thanh "Lot dem ki vao." (demo data).

### 3. Sinh don NGAU NHIEN (OrderGenerator) thay boc tu OrderPool
- OrderPool chi con la du lieu mau tuy chon; don sinh qua state.Rng voi thu tu lay random CO DINH: (1) cang di (trong so x2 khi co tau Idle o do) -> (2) cang den (bat buoc co tuyen) -> (3) tung tag theo orderTagChance -> (4) phan han cong them -> (5) jitter doanh thu -> (6) ten khach -> (7) manh moi. Id "O-G{n}".
- Don DAU TIEN (OrderSpawnCount==0) theo luat "de": cang di dang co tau Idle, tuyen ngan nhat <=1 route-unit, khong tag, han = ETA + bien phan ung + bien huong dan CHINH XAC (khong cong them roll ngau nhien - test DonDau_DungLuatDe da bat bug nay).
- NHIP sinh (OrderScheduler): moc dau = orderFirstDelaySeconds; interval sau do = orderIntervalSeconds x decay^OrderSpawnCount (exponent = so don da sinh TRUOC luot nay - don dau giu nguyen 40s), san orderIntervalMinSeconds. Chong tran hop thu: du maxInboxOrders -> bo luot sinh, van di moc.
- Goc khi hang: NewRun dat NextOrderSpawnAtSeconds = orderFirstDelaySeconds (TRUOC DO la 0f -> don sinh ngay tick dau, don dau chon cang co tau Idle -> demand ghim tau tai cang -> 6 test RoamTests bao "Idle thay vi Roaming").

### 4. Balance TRUOC vs SAU (400 van/policy, seedBase 1000, DemoBuilder.Balance)
| Chinh sach | Truoc | Sau |
|---|---|---|
| Ngau nhien | 34% (135/400) [E1 5 · E2 29 · E3 101 · E4 265] | 11% (44/400) [E1 3 · E2 14 · E3 27 · E4 355 · E5 1] |
| Doc manh moi | 79% (316/400) [E1 55 · E2 167 · E3 94 · E4 84] | 73% (292/400) [E1 70 · E2 127 · E3 95 · E4 108] |
| Thoi luong TB | 284s / 324s | 284s / 358s |
| p10/p50/p90 | 207/286/356 · 272/328/374 | 222/287/342 · 315/359/407 |
| Tre TB | 2.57 / 1.52 | 2.84 / 1.60 |
| Ket | 0/400 / 0/400 | 0/400 / 0/400 |
- Ty le giam (34->11, 79->73) la CHU TAM CUA THIET KE: don ngau nhien kho hon bo don tutorial cu (tuyen dai hon, tag nhieu hon, han chat hon). Policy doc manh moi van 73% = do kho phu hop voi nguoi choi co chien luoc; policy ngau nhien 11% = roi vao E4 la chinh (do tre 3 don) - neu muon nho hon, giam orderDeadlineExtraMinSeconds hoac tang maxActiveOrders, KHONG sua generator.

### 5. Test: 118/118 EditMode PASS (chi co thay doi test duoc li do)
- SimulationTests / Day1FogRouteTests / RouteProgressTests: don tutorial cu O-01/O-02 dung tay qua TestWorld.AddManualOrder (G7 bo SpawnInitial don co san; SpawnInitial chi dat moc).
- RoamTests: chong spawn bang orderFirstDelaySeconds=100000f; test 4 dung AddManualOrder; test 8 REWRITE - bat bien doi tu "noi dung don giong het" thanh "nhip sinh giong het (OrderSpawnCount + NextOrderSpawnAtSeconds)" vi don ngau nhien phu thuoc vi tri tau (trong so cang co tau Idle); phia roam-tat dung Object.Instantiate(config) copy (config dung chung tham chieu).
- OrderSchedulerTests: decay 0.5 them vao test Inbox; ky vong moc 25->65->85->100 (interval 40x0.5^n san 15).
- OrderGeneratorTests (moi): cung seed cung chuoi; han >= ETA + bien; Id duy nhat 200 lan; don dau dung luat de; tag/doanh thu trong khoang config.
- StuckClockTests (moi): sweep seed 1..50 x 2 policy + bat bien 1500s + tau dang cho tro don Assigned that.
- CommandValidatorTests / DemoValidatorTests: them test Phan 2 + tham so generator (max>=min, roamStart>=moc lam quen).
- PlayMode TerminalInputTests: khong tham chieu don hang -> chay nguyen ban (chay rieng).
- Skip (khong vong doi): Fleet routeEtaLabels cho tau Roaming dang cho hang - scope creep, ghi o day.

### 6. DemoValidator them kiem (G7)
- orderDeadlineExtraMaxSeconds >= orderDeadlineExtraMinSeconds; orderRevenueBase > 0; roamStartSeconds >= orderFirstDelaySeconds; toan bo cap cang trong RouteTable phai co tuyen (generator co the sinh don vao cap nao do).
