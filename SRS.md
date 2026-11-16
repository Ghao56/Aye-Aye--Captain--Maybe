# SRS — Aye Aye, Boss... Maybe? *(tên tạm)*

> **Tài liệu tự chứa (self-contained), hợp nhất toàn bộ SRS cũ + ASSUMPTIONS.** Đọc xong không cần hỏi thêm gì về project.
> Thay thế: `SRS.md` v2 (game cũ "Aye Aye, Captain" trước pivot), `SRS_v3_Aye_Aye_Boss.md` (luật "8 ngày / Uy tín" đã bỏ), `SRS_v4.md`, và `ASSUMPTIONS.md` (nhật ký sai khác 13–16/11/2026 — toàn bộ đã hợp nhất tại đây). Bốn file trên đã xóa.

| Mục | Giá trị |
|---|---|
| Sự kiện | Week Sauce · 11/2026 · Chủ đề **"Make It So"** |
| Nền tảng | Unity 6000.3 · C# · UI Toolkit (UIDocument world-space) · Input System · WebGL / itch.io |
| Trạng thái | **Chạy được gần đầy đủ trong Editor** (mốc G0–G7, 118 test EditMode pass). Chưa build WebGL, chưa playtest người thật |
| Ngôn ngữ | Tiếng Việt; mã FR/NFR và identifier giữ tiếng Anh |
| Ưu tiên | **[M]** Must · **[S]** Should (nếu còn thời gian) |
| Mục đích tài liệu | Cho AI/agent: (1) review game design & balance, (2) tư vấn tiếp triển khai phần còn dở |

---

## 1. Game là gì

Bạn là sếp một công ty vận tải biển thu nhỏ. Cửa sổ chơi là một **terminal CRT world-space** (thẩm mỹ máy tính thập niên 1960): bấm vào máy → camera zoom vào màn hình kính → game hiện; `Esc` → lùi ra (game **không dừng**, chỉ không nhìn thấy). **Không có màn BẮT ĐẦU**: máy bật → ván chạy ngay (màn `start-screen` chỉ còn dùng cho màn kết thúc: title + nút CHƠI LẠI).

Giao diện trong kính gồm: thanh trên (Thời gian · Trễ x/3 · Đã giao n/10 · Doanh thu), 3 tab — **ĐƠN HÀNG** (inbox + đơn đã nhận), **ĐỘI TÀU** (bản đồ hình sao + thẻ tàu + ra lệnh), **SỰ CỐ** (giải quyết sự cố) — và log/thoại. **Không có nút MAKE IT SO làm điểm chốt**: bấm nhận đơn/giao lệnh là có hiệu lực ngay, không hủy được.

Chơi 8–12 phút, chỉ chuột, một ván, không lưu.

## 2. Luật chơi hiện tại (bản chốt sau pivot + G7)

### 2.1 Đồng hồ & vòng chơi
- Ván đo bằng **giây mô phỏng** `ElapsedSeconds`, xác định theo seed (không đọc đồng hồ hệ thống). UI cộng dồn dt thật rồi gọi `Advance(state, dt)` theo bước cố định `simTickSeconds = 0.1` (clamp dt khi mất focus; pause khi mất focus hoặc khi có sự cố chờ — topbar hiện rõ lý do: TAM DUNG (mất focus) / TAM DUNG (sự cố chờ) / DANG CHAY / KET THUC, cập mỗi frame).
- Tốc độ tàu theo route-unit: `secondsPerRouteUnit = 20` ở tốc Thường; Fast 1.5× · Normal 1.0× · Slow 0.7×.
- **Bỏ hẳn khái niệm Ngày/Pha Planning–Simulation–Incident–Summary** và bỏ `StagedCommands`: mọi lệnh có hiệu lực lập tức, **không hủy được**.

### 2.2 Đơn hàng — sinh NGẪU NHIÊN (G7, thay rút từ OrderPool)
- `OrderPool` chỉ còn là **dữ liệu mẫu tùy chọn**; đơn sinh qua `OrderGenerator` dùng `state.Rng` với **thứ tự lấy random CỐ ĐỊNH**: (1) cảng đi (trọng số ×2 khi có tàu Idle ở đó) → (2) cảng đến (bắt buộc có tuyến) → (3) từng tag theo `orderTagChance` → (4) phần hạn cộng thêm → (5) jitter doanh thu → (6) tên khách → (7) manh mối. Id `O-G{n}`.
- **Đơn ĐẦU TIÊN** (`OrderSpawnCount == 0`) theo luật "dễ": cảng đi đang có tàu Idle, tuyến ngắn nhất ≤ 1 route-unit, không tag, hạn = ETA + biên phản ứng + biên hướng dẫn **chính xác** (không cộng thêm roll ngẫu nhiên).
- **Nhịp sinh**: mốc đầu = `orderFirstDelaySeconds` (không còn đơn có sẵn lúc 0s); sau đó interval = `orderIntervalSeconds` × `orderIntervalDecayPerOrder^OrderSpawnCount` (mặc định 40 s × 0.92ⁿ), sàn `orderIntervalMinSeconds = 15`. Hộp thư đầy (`maxActiveOrders = 4`) → bỏ lượt sinh, mốc vẫn chạy.
- Hạn đơn = **giây** (`deadlineSeconds` tính từ lúc đơn xuất hiện). Đơn Accepted/Assigned quá hạn → "trễ" chốt **MỘT LẦN**, `LateCount++`. Giao muộn vẫn được: doanh thu ×`lateRevenueFactor = 0.5`. Đơn trong inbox quá hạn → xóa, không tính trễ.
- **Doanh thu là thước đo phụ duy nhất. Uy tín (Reputation) đã BỎ hoàn toàn.**

### 2.3 Thắng / thua / kết cục
- **Thắng**: `DeliveredCount >= winDeliveries = 10`. **Thua**: `LateCount >= maxLateDeliveries = 3`.
- Kết cục: thắng 0 trễ → **E1**, 1 trễ → **E2**, ≥2 trễ → **E3**; thua → **E4** (đã giao ≥ 2) hoặc **E5** (đã giao < 2). Văn bản 5 kết cục giữ tinh thần hài cũ (đế chế "Sếp Vĩ Đại" → phá sản, cả đội đòi lương bằng bánh).

### 2.4 Sự cố
- Roll khi tàu **vừa vào cạnh mới** (kể cả tick xuất phát): xác suất `incidentChancePerEdge = 0.15`, tối đa `maxPendingIncidents = 2` cùng lúc; hazard lấy của cạnh hiện tại.
- **I-01 (Sương mù) bắt buộc lần ĐẦU TIÊN** một tàu vào cạnh khớp trigger; **I-F (finale)** một lần khi `DeliveredCount == winDeliveries - 1` và có tàu đang chở hàng.
- Sự cố chờ → **đồng hồ DỪNG** (`CanAdvance == false`) cho đến khi người chơi chọn phương án xong. Chọn xong hiện ngay outcome + lời giải thích, không undo. **UI tự mở tab SỰ CỐ** khi pending chuyển 0→>0 (fix G6.3b: trước đó người chơi ở tab ĐỘI TÀU chỉ thấy đồng hồ đứng).
- Không roll sự cố khi tàu **roaming** (`incidentsWhileRoaming = false`).

### 2.5 Roaming (G5)
- Tàu `Idle` (đậu cảng), không chở hàng, là **tàu dư thừa** (số tàu đậu > số đơn chờ lấy tại cảng đó), đã qua ân hạn `roamStartSeconds = 30` và hết nghỉ `dwellSeconds` → tự đi 1 cạnh tới cảng kề, tốc ×`roamSpeedFactor = 0.7`. Tới đích về Idle, đặt lại timer.
- Người chơi **không điều khiển** tàu roaming; tàu Roaming bị từ chối Assign/Reposition (thông báo đích đến + giây còn lại).
- Roaming dùng RNG riêng `RoamRng` (seed ^ 0x5F5F5F5F) — không đụng `state.Rng` nên **không ảnh hưởng chuỗi đơn/sự cố** (test chứng minh).
- Hồ sơ tính cách `RoamProfile` (giá trị khởi điểm, chưa chốt): Bob nghỉ ngắn thích cảng xa; Chen nghỉ lâu thích cảng gần + gắn HQ; Mira gắn HQ đi đều; Dex nhiễu cao hay đổi ý.

### 2.6 Nguyên tắc manh mối & bớt giới hạn (G7)
- Mọi lệnh phải **đọc được từ manh mối** hiển thị (tag đơn, tính cách thuyền trưởng, hazard tuyến, tình trạng tàu/cờ); không có outcome ngẫu nhiên vô lý.
- **BỎ chặn tàu theo thuyền trưởng/tag**: `CommandValidator.ValidateAssign` không còn kiểm tra captain/tag — manh mối chỉ còn là hướng dẫn, không còn cảnh báo cứng. (Manh mối O-04 trong demo data đã đổi accordingly, bỏ nhắc "Dùng cho Bob".)

## 3. Dữ liệu game (như đã dựng trong repo)

### 3.1 Bản đồ hình sao — 5 nút, 6 cạnh
`goc` = Trạm Trung Chuyển (HQ, hình vuông giữa bản đồ) + 4 hành tinh: `vang`, `suong`, `camap`, `tram`. Mỗi cặp cảng ≤ 2 RouteOption; 10/10 cặp đều có tuyến (test chốt). `PortDef.mapPosition` (Vector2 0..1) nằm trong asset, C# không hard-code tọa độ.

| Cạnh | Route-units | Hazard |
|---|---|---|
| goc–vang | 1 | Clear |
| goc–suong | 1 | Fog |
| goc–camap | 2 | Pirates |
| goc–tram | 2 | Ice |
| suong–camap | 1 | Fog |
| camap–tram | 1 | Storm |

### 3.2 Tàu (4)
| Tàu | Thuyền trưởng | Speed | Tag |
|---|---|---|---|
| CARGO-9 | Bob | Fast | Nhanh · Ẩu · Thích đường tắt |
| HAULER-4 | Chen | Slow | Chậm · Thân vững · Bình tĩnh |
| FREIGHT-12 | Mira | Normal | Chuẩn sách · Hơi cứng nhắc |
| TRANSIT-3 | Dex | Normal | Đa nghi · Kiểm tra kỹ |

### 3.3 Đơn hàng
Từ G7 đơn **sinh ngẫu nhiên** theo mục 2.2; bảng đơn cố định O-01..O-08 cũ chỉ còn trong `OrderPool` (dữ liệu mẫu). Tham số sinh nằm trong `GameConfig`/`DemoData` (kiểm biên qua `DemoValidator`: `orderDeadlineExtraMaxSeconds ≥ Min`, `orderRevenueBase > 0`, mọi cặp cảng có tuyến, `roamStartSeconds ≥ orderFirstDelaySeconds`).

### 3.4 Sự cố (8) — bảng khóa (thuyền trưởng × phương án → Outcome); không khớp bảng = Normal
| ID | Trigger | Phương án | Smart | Normal | Slip-up | Hệ quả đặc biệt |
|---|---|---|---|---|---|---|
| **I-01 Sương mù dày** | bắt buộc lần ĐẦU vào cạnh Fog | A. Giữ tốc · B. Giảm tốc dò · C. Theo tín hiệu cảng | Mira+C · Chen+B | any+B | Bob+A · Dex+C | Bob+A đặt `CargoShifted` (nếu có hàng) |
| **I-02 Bão** | cạnh Storm | A. Xuyên bão · B. Neo chờ · C. Vòng tránh | Chen+A · Mira+C | any+B | Bob+A · Dex+C | Slip-up: Condition → Hao mòn |
| **I-03 Hải tặc** | cạnh Pirates | A. Trả phí qua đường · B. Phóng nhanh thoát · C. Mặc cả/đánh lừa | Dex+C · Bob+B | any+A | Chen+B · Mira+C | any+A: −20 Doanh thu |
| **I-04 Kẹt cảng** | cập cảng đến/đích trung gian | A. Xếp hàng chờ · B. Nhờ quan hệ chen ngang · C. Chuyển bến phụ | Bob+B · Mira+C | any+A | Dex+B · Chen+C | — |
| **I-05 Hàng rung lắc** | Hàng Dễ vỡ | A. Giảm tốc · B. Chằng lại giữa biển · C. Mặc kệ | Mira+B | any+A · Chen+C | Bob+B · (any trừ Chen)+C | Slip-up: Cargo −1 mức |
| **I-06 Máy móc hỏng** | Condition = Hao mòn hoặc `TemporaryRepair=true` (×3) | A. Sửa chuẩn · B. Sửa tạm (nhanh) | Mira+A · Bob+B | Chen+A · Bob+A | Dex+B | Bob+B đặt `TemporaryRepair=true` |
| **I-07 Tín hiệu cầu cứu** | bất kỳ | A. Bỏ qua · B. Đi giúp · C. Kiểm tra tín hiệu | Bob+B (nhận quà) | any+A · Mira+C | Dex+C · Chen+B | — |
| **I-F Cuộc gọi Chủ tịch** | khi còn 1 đơn cuối để thắng + có tàu chở hàng | A. Ép hết tốc lực · B. Giữ nhịp · C. Xin gia hạn khách | Mira+C · Bob+A (chỉ khi không có cờ) | Chen+B | Dex+C · Bob+A khi `TemporaryRepair=true` | `CargoShifted` + A → Cargo −1 mức |

Cargo 3 mức Intact/Scratched/Ruined; tối đa 1 cờ mỗi sự cố; Cargo/Condition chỉ đổi tại điểm ghi rõ trong `IncidentDef`. **Hiệu lực thời gian của I-06 chưa đặc tả chặt** (xem mục 6).

## 4. Kiến trúc & ràng buộc kỹ thuật

- **Logic/Data thuần C# (không phụ thuộc UnityEngine)**: `GameState`, `Simulation.Advance`, `OrderScheduler`, `OrderGenerator`, `IncidentScheduler`, `IncidentResolver`, `CommandValidator`/`FleetActions`/`OrderActions`, `ScoreCalculator`, `EndingEvaluator`, `RoamPlanner`, `RouteProgress`, `CluePolicy`, `BalanceProbe`, `DemoValidator`. UI chỉ render + gửi intent, không tự tính kết quả.
- **Mọi ngẫu nhiên qua seed**: `state.Rng` (đơn + sự cố) và `RoamRng` (roaming) tách riêng → tái lập được.
- **Dữ liệu = ScriptableObject**: `GameConfig`, `ShipDef` (kèm captain, `RoamProfile`, startPort), `PortDef` (kèm `mapPosition`), `RouteDef`, `RouteTable`, `OrderDef`/`OrderPool` (mẫu), `IncidentDef`/`IncidentPool`, `EndingDef`. Không hard-code số cân bằng ngoài data.
- **UI (UI Toolkit trong `#screen` của panel world-space)**:
  - Tab ĐỘI TÀU **2 cột (G6.2)**: trái = bản đồ (min-height 300px) + khối "ĐƠN CHỜ ĐIỀU TÀU" cuộn được; phải = ScrollView thẻ tàu 1 cột + thanh lệnh. Mọi vùng `overflow: hidden`, cuộn trong vùng của mình — không bao giờ vẽ đè. Scroll giữ vị trí qua Refresh (mẫu `SavedScroll`). Font ≥ 20px, 4 màu phosphor.
  - **Giao đơn ≤ 3 bấm (G6.3)**: bấm đơn → tự chọn tàu nếu đúng 1 tàu đủ điều kiện → bấm nút tuyến = `FleetActions.Assign` NGAY. Nhãn nút: tuyến ngắn nhất = NHANH, còn lại = AN TOÀN; hazard contextual "! Sương mù/Bão/Cướp/Băng". Đơn đã giao → reset cả chọn đơn lẫn tàu. Chấm tàu mờ theo đơn không khớp.
  - **ETA sống 10 Hz** cho tàu Moving lẫn Roaming (`shipEtaLabels`, ưu tiên "còn Ns" hơn %).
  - Tự phục hồi: `lastRefreshKey` ghi SAU khi Refresh chạy trọn → UI tự hồi phục sau exception (G7). Chỉ dẫn cách chơi đầu tab ĐƠN HÀNG, tự ẩn sau lần nhận đầu tiên.
- **Công cụ Editor**: `Tools/AyeAye/Build Demo`, `Validate`, `Balance` (400 ván × 2 chính sách), `PlayCapture` (driver tự chơi + chụp ảnh mốc), `FleetClickCheck` (kiểm picking/overlap trong Play mode).
- **Kiểm thử**: 118 test EditMode pass (scheduler, generator, resolver, validator, Simulation flow, RoamRng, DemoValidator, BalanceProbe, StuckClock sweep seed 1..50). PlayMode: TerminalInputTests + FleetClickCheck.

## 5. Trạng thái triển khai

| Phần | Trạng thái |
|---|---|
| Terminal world-space + CRT + zoom + Esc, UI Toolkit, 3 tab, topbar | **Xong** (G2) |
| Tab ĐƠN HÀNG: inbox, accept/decline, giới hạn 4 đơn, hết hạn inbox, hạn theo giây, đếm ngược đơn đầu | **Xong** |
| Tab ĐỘI TÀU: bản đồ hình sao, thẻ tàu, assign/reposition hiệu lực ngay, G6.2 2 cột, G6.3 ≤3 bấm + ETA sống | **Xong** |
| Simulation real-time tick 0.1s, giao hàng tự động, roaming | **Xong** (G1+G5) |
| Tab SỰ CỐ: roll theo cạnh, UI resolve, đồng hồ dừng, **tự mở tab** | **Xong** (G2+G6.3b) |
| Sinh đơn ngẫu nhiên (OrderGenerator) + bỏ chặn tàu theo captain/tag | **Xong** (G7) |
| Thắng/thua + 5 kết cục + màn ending + restart | **Xong** |
| BalanceProbe + PlayCapture + Validate + Build Demo | **Xong** (số balance sau G7: mục 5.1) |
| Build WebGL, nộp itch.io, NFR-11 (credit asset/font, khai báo AI) | **Chưa làm** |
| Playtest người thật (2–3 người) | **Chưa làm** — điều kiện trước khi siết balance |
| Lệnh Repair riêng (FR-0408 cũ), nhật ký sự cố cũ, nút thoát máy trên UI, sự cố khi roaming | **Bỏ / không làm** |

### 5.1 Balance SAU G7 (400 ván/policy, seedBase 1000, DemoBuilder.Balance)
| Chính sách | Trước G7 | Sau G7 |
|---|---|---|
| Ngẫu nhiên | 34% | **11%** (44/400) [E1 3 · E2 14 · E3 27 · E4 355 · E5 1] |
| Đọc manh mối | 79% | **73%** (292/400) [E1 70 · E2 127 · E3 95 · E4 108] |
| Thời lượng TB | 284s / 324s | 284s / 358s |
| p10/p50/p90 | 207/286/356 · 272/328/374 | 222/287/342 · 315/359/407 |
| Trễ TB | 2.57 / 1.52 | 2.84 / 1.60 |

Tỷ lệ giảm là **chủ tâm thiết kế**: đơn ngẫu nhiên khó hơn bộ đơn tutorial cũ. Policy đọc-manh-mối 73% = độ khó phù hợp người chơi có chiến lược; policy ngẫu nhiên 11% rơi vào E4 chủ yếu do trễ 3 đơn — nếu muốn nới, đổi `orderDeadlineExtraMinSeconds` hoặc `maxActiveOrders`, **KHÔNG sửa generator**.

## 6. Câu hỏi đang cần tư vấn

**Design/balance:**
1. Bộ núm nào siết trước sau playtest thật? Ứng viên: `incidentChancePerEdge` 0.15→0.25 (không đụng validator), `deadlineSeconds`/biên hạn sinh, `orderIntervalSeconds`, `maxLateDeliveries` 3→2.
2. Roaming có phá puzzle không? `RoamProfile` cần chốt theo đo thực.
3. **Hiệu lực thời gian các phương án I-06** (Sửa chuẩn/Sửa tạm) chưa chặt — "mất 1 ngày" là luật thế giới cũ theo ngày. Đổi thành gì: trừ dt cố định? Condition chỉ phục khi quay cảng?
4. Vòng chơi real-time đủ chiều sâu chưa (4 tàu × 1 lệnh/chuyến, thắng 10 đơn)? Có cần thêm quyết định chiến lược?

**Triển khai phần còn dở:**
5. Thứ tự tới nộp: (a) playtest thật, (b) đo lại/điều chỉnh balance, (c) build WebGL + itch.io, (d) các mục [S]. Đề xuất kế hoạch?
6. Cần bổ gì tối thiểu để nộp đạt (NFR-11 credit asset, kiểm tra font dấu tiếng Việt trên WebGL…)?

## 7. NFR (giữ từ v3)
WebGL < 40 MB, ≥30 fps, chỉ chuột + Esc, không softlock (test mô phỏng, chặn 1800s), tiếng Việt đủ dấu (font JetBrains Mono OFL), không save dài hạn, ghi nguồn asset/AI khi submit.

## 8. Phạm vi loại trừ (không làm)
Điều khiển tàu trực tiếp; pathfinding thời gian chạy; tiền để chi tiêu/mua tàu; multiplayer; lưu dài hạn; hội thoại phân nhánh; chuyển hàng giữa tàu; hủy đơn đã nhận.

## 9. Truy vết tài liệu
- Tài liệu cũ (`SRS.md` v2, `SRS_v3_Aye_Aye_Boss.md`, `SRS_v4.md`, `ASSUMPTIONS.md`) **đã xóa 16/11/2026** — nội dung còn hiệu lực được hợp nhất tại đây.
- Lịch sử chi tiết từng mốc G0–G7 nằm trong git history (tag `day-based-final` = điểm trước bỏ ngày).
