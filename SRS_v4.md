# SRS v4 — Aye Aye, Boss... Maybe? *(tên tạm)*

> **Tài liệu tự chứa (self-contained).** Đọc xong không cần hỏi thêm gì về project.
> Thay thế SRS v3 (`SRS_v3_Aye_Aye_Boss.md`) — v3 đã lỗi trọng vì game **pivot 2 lần sau khi v3 viết**; các sai khác từng ghi rải rác trong `ASSUMPTIONS.md`, giờ hợp nhất tại đây. Bản v3/v2 giữ lại chỉ để truy vết lịch sử.

| Mục | Giá trị |
|---|---|
| Sự kiện | Week Sauce · 11/2026 · Chủ đề **"Make It So"** |
| Nền tảng | Unity 6000.3 · C# · UI Toolkit (UIDocument world-space) · Input System · WebGL / itch.io |
| Trạng thái | **Đã chạy được gần đầy đủ trong Editor** (mốc G0–G5, 96 test EditMode pass). Chưa build WebGL, chưa playtest người thật |
| Ngôn ngữ | Tiếng Việt; mã FR/NFR và identifier giữ tiếng Anh |
| Ưu tiên | **[M]** Must · **[S]** Should (nếu còn thời gian) |
| Mục đích tài liệu | Cho AI/agent: (1) **review game design & balance**, (2) **tư vấn tiếp triển khai** phần còn dở |

---

## 1. Game là gì

Bạn là sếp một công ty vận tải biển thu nhỏ. Cửa sổ chơi là một **terminal CRT world-space** (thẩm mỹ máy tính thập niên 1960): bấm vào máy → camera zoom vào màn hình kính → game hiện; `Esc` → lùi ra (game **không dừng**, chỉ không nhìn thấy).

Giao diện trong kính gồm: thanh trên (Thời gian · Trễ x/3 · Đã giao n/10 · Doanh thu), 3 tab — **ĐƠN HÀNG** (inbox + đơn đã nhận), **ĐỘI TÀU** (bản đồ hình sao + thẻ tàu + ra lệnh), **SỰ CỐ** (giải quyết sự cố) — và thanh dưới có log/thoại. **Không còn nút MAKE IT SO làm điểm chốt**: bấm nhận đơn/giao lệnh là có hiệu lực ngay.

Chơi 8–12 phút, chỉ chuột, một ván, không lưu.

## 2. Luật chơi hiện tại (bản chốt sau pivot — thay toàn bộ luật "8 ngày / Uy tín" của v3)

### 2.1 Đồng hồ & vòng chơi
- Ván đo bằng **giây mô phỏng** `ElapsedSeconds`, xác định theo seed (không đọc đồng hồ hệ thống). UI cộng dồn dt thật rồi gọi `Advance(state, dt)` theo bước cố định `simTickSeconds = 0.1` (clamp dt khi mất focus; pause khi mất focus hoặc khi có sự cố chờ).
- Tốc độ tàu theo route-unit: `secondsPerRouteUnit = 20` ở tốc Thường; Fast 1.5× · Normal 1.0× · Slow 0.7×. Tàu trong tab ĐỘI TÀU di chuyển theo thời gian thật.
- **Bỏ hẳn khái niệm Ngày/Pha Planning–Simulation–Incident–Summary** và bỏ `StagedCommands`: mọi lệnh có hiệu lực lập tức, **không hủy được** (HỦY LỆNH chỉ hủy lệnh vừa staged của tàu đang đứng, trước khi nó rời cảng).

### 2.2 Đơn hàng
- Spawn: O-01/O-02 (tutorial) lúc 0 giây; sau đó 1 đơn mỗi `orderIntervalSeconds = 40`, rút ngắn ×`orderIntervalDecayPerOrder = 0.92` theo số đơn đã bốc, sàn 15s. Rút từ `OrderPool` theo trọng số, không lặp trong ván (hết pool thì cho lặp — không bao giờ kẹt).
- `maxActiveOrders = 4` (inbox + đã nhận).
- Hạn đơn = **giây** (`deadlineSeconds` tính từ lúc đơn xuất hiện). Quá hạn: đơn chuyển "trễ" chốt MỘT LẦN, `LateCount++`. Giao muộn vẫn được: doanh thu ×0.5. Đơn trong inbox quá hạn → xóa, không tính trễ.
- **Doanh thu là thước đo phụ duy nhất. Uy tín (Reputation) đã BỎ hoàn toàn** — không field, không hiển thị, không phạt.

### 2.3 Thắng / thua / kết cục
- **Thắng**: `DeliveredCount >= winDeliveries = 10`. **Thua**: `LateCount >= maxLateDeliveries = 3`.
- Kết cục theo kết quả: thắng 0 trễ → **E1**, 1 trễ → **E2**, ≥2 trễ → **E3**; thua → **E4** (đã giao ≥ 2) hoặc **E5** (đã giao < 2). Văn bản 5 kết cục theo tinh thần hài cũ (đế chế "Sếp Vĩ Đại" → phá sản, cả đội đòi lương bằng bánh).

### 2.4 Sự cố
- Roll khi tàu **vừa vào cạnh mới** (kể cả tick xuất phát): xác suất `incidentChancePerEdge = 0.15`, tối đa `maxPendingIncidents = 2` cùng lúc; hazard lấy của cạnh hiện tại.
- **I-01 (Sương mù) bắt buộc lần ĐẦU TIÊN** một tàu vào cạnh khớp trigger; **I-F (finale)** một lần khi `DeliveredCount == winDeliveries - 1` và có tàu đang chở hàng.
- Sự cố chờ → **đồng hồ DỪNG** (`CanAdvance == false`) cho đến khi người chơi chọn phương án xong. Chọn xong hiện ngay outcome + lời giải thích, không undo.
- Không roll sự cố khi tàu **roaming** (`incidentsWhileRoaming = false`).

### 2.5 Roaming (G5 — tàu rảnh tự đi)
- Tàu `Idle` (đậu cảng), không chở hàng, là **tàu dư thừa** (số tàu đậu > số đơn chờ lấy tại cảng đó), và đã qua ân hạn `roamStartSeconds = 30` + hết nghỉ `dwellSeconds` → tự đi 1 cạnh tới cảng kề, tốc ×`roamSpeedFactor = 0.7`. Tới đích về Idle, đặt lại timer.
- Người chơi **không điều khiển** tàu roaming; tàu Roaming bị từ chối Assign/Reposition (thông báo đích đến + giây còn lại).
- Roaming dùng RNG riêng `RoamRng` (seed ^ 0x5F5F5F5F) — không đụng `state.Rng` nên **không ảnh hưởng chuỗi đơn/sự cố** (có test chứng minh).
- Hồ sơ tính cách `RoamProfile` (giá trị khởi điểm, chưa chốt): Bob nghỉ ngắn thích cảng xa; Chen nghỉ lâu thích cảng gần + gắn HQ; Mira gắn HQ đi đều; Dex nhiễu cao hay đổi ý.

### 2.6 Nguyên tắc manh mối
Mọi lệnh phải **đọc được từ manh mối** hiển thị (tag đơn, tính cách thuyền trưởng, hazard tuyến, tình trạng tàu/cờ); không có outcome ngẫu nhiên vô lý. Người chơi suy luận được, kể cả khi vẫn có thể sai.

## 3. Dữ liệu game (như đã dựng trong repo)

### 3.1 Bản đồ hình sao — 5 nút, 6 cạnh (thay §5.1–5.2 của v3)
`goc` = Trạm Trung Chuyển (HQ, hình vuông giữa bản đồ) + 4 hành tinh: `vang`, `suong`, `camap`, `tram`. **Đã bỏ Mỏm Gió.**

| Cạnh | Route-units | Hazard |
|---|---|---|
| goc–vang | 1 | Clear |
| goc–suong | 1 | Fog |
| goc–camap | 2 | Pirates |
| goc–tram | 2 | Ice |
| suong–camap | 1 | Fog |
| camap–tram | 1 | Storm |

Mỗi cặp cảng ≤ 2 RouteOption; 10/10 cặp đều có tuyến (test chốt).

### 3.2 Tàu (4)
| Tàu | Thuyền trưởng | Speed | Tag |
|---|---|---|---|
| CARGO-9 | Bob | Fast | Nhanh · Ẩu · Thích đường tắt |
| HAULER-4 | Chen | Slow | Chậm · Thân vững · Bình tĩnh |
| FREIGHT-12 | Mira | Normal | Chuẩn sách · Hơi cứng nhắc |
| TRANSIT-3 | Dex | Normal | Đa nghi · Kiểm tra kỹ |

### 3.3 Đơn hàng (hạn theo giây, đã đo biên qua DemoValidator)
| Đơn | Khách | Hàng | Tuyến | Hạn (s) | DT |
|---|---|---|---|---|---|
| O-01 | Tiệm Bánh Hành Tinh Xa | Dễ vỡ | goc→vang | 70 | 120 |
| O-02 | Cửa hàng Dép Lê Số 1 | Thường | goc→camap | 85 | 100 |
| O-03 | Kem Que Sa Mạc | Lạnh | goc→tram | 75 | 90 |
| O-04 | Tiệm Gương Vỡ Hạnh Phúc | Dễ vỡ | suong→camap | 45 | 110 |
| O-05 | Pizza Giao Chậm Là Miễn Phí | Khẩn | goc→vang | 45 | 70 |
| O-06 | Hội Sưu Tập Đá Cuội | Thường | vang→tram | 90 | 100 |
| O-07 | Đám Cưới Cá Heo | Dễ vỡ + Khẩn | camap→goc | 90 | 160 |
| O-08 | Bưu Kiện Không Ghi Tên | Thường | suong→tram | 75 | 130 |

### 3.4 Sự cố (8) — bảng khóa (thuyền trưởng × phương án → Outcome); không khớp bảng = Normal
[Bob] CARGO-9 · [Chen] HAULER-4 · [Mira] FREIGHT-12 · [Dex] TRANSIT-3 · "any" = bất kỳ.

| ID | Trigger | Phương án | Smart | Normal | Slip-up | Hệ quả đặc biệt |
|---|---|---|---|---|---|---|
| **I-01 Sương mù dày** | bắt buộc lần ĐẦU vào cạnh Fog | A. Giữ tốc · B. Giảm tốc dò · C. Theo tín hiệu cảng | Mira+C · Chen+B | any+B | Bob+A · Dex+C | Bob+A đặt `CargoShifted` (nếu có hàng) |
| **I-02 Bão** | cạnh Storm | A. Xuyên bão · B. Neo chờ · C. Vòng tránh | Chen+A · Mira+C | any+B | Bob+A · Dex+C | Slip-up: Condition → Hao mòn |
| **I-03 Hải tặc** | cạnh Pirates | A. Trả phí qua đường · B. Phóng nhanh thoát · C. Mặc cả/đánh lừa | Dex+C · Bob+B | any+A | Chen+B · Mira+C | any+A: −20 Doanh thu |
| **I-04 Kẹt cảng** | cập cảng đến/đích trung gian | A. Xếp hàng chờ · B. Nhờ quan hệ chen ngang · C. Chuyển bến phụ | Bob+B · Mira+C | any+A | Dex+B · Chen+C | — |
| **I-05 Hàng rung lắc** | Hàng Dễ vỡ | A. Giảm tốc · B. Chằng lại giữa biển · C. Mặc kệ | Mira+B | any+A · Chen+C | Bob+B · (any trừ Chen)+C | Slip-up: Cargo −1 mức |
| **I-06 Máy móc hỏng** | Condition = Hao mòn hoặc `TemporaryRepair=true` (×3) | A. Sửa chuẩn (mất 1 ngày*) · B. Sửa tạm (nhanh) | Mira+A · Bob+B | Chen+A · Bob+A | Dex+B | Bob+B đặt `TemporaryRepair=true` |
| **I-07 Tín hiệu cầu cứu** | bất kỳ | A. Bỏ qua · B. Đi giúp · C. Kiểm tra tín hiệu | Bob+B (nhận quà) | any+A · Mira+C | Dex+C · Chen+B | — |
| **I-F Cuộc gọi Chủ tịch** | khi còn 1 đơn cuối để thắng + có tàu chở hàng | A. Ép hết tốc lực · B. Giữ nhịp · C. Xin gia hạn khách | Mira+C · Bob+A (chỉ khi không có cờ) | Chen+B | Dex+C · Bob+A khi `TemporaryRepair=true` | `CargoShifted` + A → Cargo −1 mức |

\* "mất 1 ngày" của bảng cũ chưa thích hợp sau pivot real-time — hiệu lực thời gian của các phương án I-06 **chưa được đặc tả chặt**, xem mục 6 (câu hỏi mở).

Cargo 3 mức Intact/Scratched/Ruined; tối đa 1 cờ mỗi sự cố; Cargo/Condition chỉ đổi tại điểm ghi rõ trong `IncidentDef`.

## 4. Kiến trúc & ràng buộc kỹ thuật (như đang thực thi trong repo)

- **Logic/Data thuần C# (không phụ thuộc UnityEngine)**: `GameState`, `Simulation.Advance`, `OrderScheduler`, `IncidentScheduler`, `IncidentResolver`, `CommandValidator`/`FleetActions`/`OrderActions`, `ScoreCalculator`, `EndingEvaluator`, `RoamPlanner`, `RouteProgress`, `CluePolicy`, `BalanceProbe`, `DemoValidator`. UI chỉ render + gửi intent, không tự tính kết quả.
- **Mọi ngẫu nhiên qua seed**: `state.Rng` (đơn + sự cố) và `RoamRng` (roaming) tách riêng → tái lập được.
- **Dữ liệu = ScriptableObject**: `GameConfig`, `ShipDef` (kèm captain, `RoamProfile`, startPort), `PortDef` (kèm `mapPosition` 0..1), `RouteDef`, `RouteTable`, `OrderDef`/`OrderPool`, `IncidentDef`/`IncidentPool`, `EndingDef`. Không hard-code số cân bằng ngoài data.
- **UI**: UI Toolkit trong `#screen` của panel world-space; CRT bật/tắt khi vào/ra máy; hiệu ứng gõ chữ `typewriterCharsPerSecond = 90` (skip được). Single scene, WebGL target.
- **Công cụ Editor**: `Tools/AyeAye/Build Demo` (dựng dữ liệu, dọn asset mồ côi), `Validate` (tham chiếu, bảng outcome đủ, 10 cặp cảng nối được, hạn đơn ≥ ETA + margin), `Balance` (400 ván × 2 chính sách), `PlayCapture` (driver tự chơi + chụp ảnh mốc).
- **Kiểm thử**: 96 test EditMode (scheduler, resolver, validator, Simulation flow, RoamRng, DemoValidator, BalanceProbe số ván nhỏ) — pass. PlayMode chỉ giữ TerminalInputTests.
- **NFR giữ nguyên từ v3**: WebGL < 40 MB, ≥30 fps, chỉ chuột + Esc, không softlock (test mô phỏng, chặn 1800s), tiếng Việt đủ dấu (font JetBrains Mono OFL), không save dài hạn, ghi nguồn asset/AI khi submit.

## 5. Trạng thái triển khai (đối chiếu FR của v3 — nguồn sự thật: repo, mốc G0–G5)

| Phần | Trạng thái |
|---|---|
| Terminal world-space + CRT + zoom + Esc, UI Toolkit, 3 tab, topbar | **Xong** (G2: polish, pause mất focus, badge, hạn đơn 10 Hz) |
| Tab ĐƠN HÀNG: inbox, accept/decline, giới hạn 4 đơn, hết hạn inbox, hạn theo giây | **Xong** |
| Tab ĐỘI TÀU: bản đồ hình sao, thẻ tàu, assign/reposition (không còn staged — hiệu lực ngay), preview ETA | **Xong** (G3+G4: đơn sắc CRT, clue cách chơi vào đầu tab) |
| Simulation real-time theo tick 0.1s, giao hàng tự động, roaming | **Xong** (G1+G5) |
| Tab SỰ CỐ: sinh sự cố theo cạnh, thẻ + UI resolve, đồng hồ dừng khi chờ | **Xong** (G2 bổ UI resolve) |
| Thắng/thua + 5 kết cục + màn ending + restart | **Xong** |
| BalanceProbe + PlayCapture + Validate + Build Demo | **Xong** (số balance sau G5 **chưa chốt lại**) |
| Build WebGL, nộp itch.io, NFR-11 (credit asset/font, khai báo AI) | **Chưa làm** |
| Playtest người thật (2–3 người) | **Chưa làm** — đang là điều kiện trước khi siết balance |
| Các mục [S] của v3 bị bỏ/do dự: lệnh Repair riêng (FR-0408), nhật ký sự cố cũ (FR-0508), nút thoát máy trên UI (FR-0205), sự cố khi roaming | **Chưa có** — Repair chỉ còn dạng phương án I-06 |

## 6. Câu hỏi đang cần tư vấn (người đọc trả lời phần này)

**Về design/balance:**
1. **Bộ núm cân bằng nào nên siết trước?** Bot đọc-manh-mối đang thắng ~96% (G4, đo trước G5 — **số này mất hiệu lực sau roaming, cần đo lại**). Ứng viên: `incidentChancePerEdge` 0.15→0.25 (không đụng validator), `deadlineSeconds` từng đơn, `orderIntervalSeconds`, `maxLateDeliveries` 3→2. Thứ tự ưu tiên đề xuất?
2. **Roaming có phá puzzle không?** Tàu rảnh tự đi làm giảm "tàu sẵn sàng chờ" — tốt cho alive-feel nhưng có làm người chơi khó hiểu tại sao tàu biến mất? `RoamProfile` cần chốt theo đo thực.
3. **Hiệu lực thời gian của các phương án I-06 (Sửa chuẩn/Sửa tạm) chưa chặt** — "Sửa chuẩn mất 1 ngày" là luật của thế giới theo ngày. Nên đổi thành gì: trừ dt cố định? Condition chỉ phục khi quay cảng? Đề xuất cơ chế giữ đúng tính chất Smart/Slip-up của bảng.
4. **Vòng chơi real-time có đủ chiều sâu không** với 4 tàu, mỗi tàu 1 lệnh mỗi chuyến, thắng 10 đơn? Có cần thêm quyết định chiến lược (vd đơn nhiều trạm, ưu tiên đơn) hay giữ tối giản cho jam?

**Về triển khai phần còn dở:**
5. Thứ tự ưu tiên tới nộp: (a) đo lại Balance sau G5, (b) playtest thật, (c) build WebGL + itch.io, (d) các mục [S]. Đề xuất kế hoạch?
6. Cần bổ gì tối thiểu để nộp đạt (NFR-11 credit asset, kiểm tra font dấu tiếng Việt trên WebGL…)?

## 7. Phạm vi loại trừ (không làm)
Điều khiển tàu trực tiếp; pathfinding thời gian chạy; tiền để chi tiêu/mua tàu; multiplayer; lưu dài hạn; hội thoại phân nhánh; chuyển hàng giữa tàu; hủy đơn đã nhận.

## 8. Truy vết tài liệu
- `SRS.md` (v2) — game cũ "Aye Aye, Captain" trước pivot, chỉ dùng truy vết lịch sử.
- `SRS_v3_Aye_Aye_Boss.md` — spec "8 ngày / Uy tín"; phần không bị mục 2–3 của bản này thay thế vẫn hiệu lực (kiến trúc, NFR, bảng sự cố, dữ liệu mẫu).
- `ASSUMPTIONS.md` — nhật ký sai khác từng ngày (13–14/11/2026), nguồn chi tiết của mục 2–3.
