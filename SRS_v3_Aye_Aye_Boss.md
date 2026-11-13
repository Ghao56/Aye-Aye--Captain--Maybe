# SRS — Software Requirements Specification
## Aye Aye, Boss... Maybe? *(tên tạm)*

| Mục | Giá trị |
|---|---|
| Tài liệu | SRS **bản 3.0** — thay thế SRS "Aye Aye, Captain... Maybe?" bản 2 (pivot thể loại) |
| Ngày lập | 12/11/2026 |
| Sự kiện | Week Sauce · Tháng 11/2026 · Chủ đề **"Make It So"** |
| Nền tảng | Unity 6000.3 · C# · UI Toolkit (UIDocument world-space) · Input System Package · WebGL / itch.io |
| Đối tượng đọc | Dev, tester, và các AI/agent phân tích, estimate, dựng backlog, viết code |
| Ngôn ngữ | Tiếng Việt (mã FR/NFR và identifier code giữ tiếng Anh để truy vết máy) |
| Ưu tiên | **[M]** Must (bắt buộc cho bản nộp) · **[S]** Should (làm nếu còn thời gian) · **[C]** Could (bỏ qua nếu thiếu thời gian) |

---

## 1. Giới thiệu

### 1.1 Mục đích tài liệu
Đặc tả đầy đủ và kiểm chứng được yêu cầu của bản nộp game jam. Mỗi yêu cầu có mã định danh (FR-xxxx / NFR-xx), mức ưu tiên và tiêu chí nghiệm thu (AC) để AI/agent có thể phân tích, đánh giá độ bao phủ và sinh test case.

### 1.2 Phạm vi sản phẩm
Game quản lý–điều phối hài hước theo ngày. Người chơi là **sếp một công ty vận tải biển**. Mỗi ngày:
1. **Tab ĐƠN HÀNG** — nhận hoặc từ chối các đơn hàng đến từ khách bên ngoài.
2. **Tab ĐỘI TÀU** — xem tình trạng và vị trí từng tàu, rồi **ra lệnh** (giao đơn, chọn tuyến, điều tàu).
3. Bấm **MAKE IT SO** để chốt lệnh → các tàu chạy.
4. **Tab SỰ CỐ** — các tàu đang chạy gặp sự cố; người chơi chọn cách xử lý.
5. Tổng kết ngày (doanh thu, Uy tín) rồi sang ngày mới.

Chơi tối đa **8 ngày**; kết cục theo **Uy tín** cuối ván. Toàn bộ giao diện nằm trong một chiếc **máy tính terminal world-space** (kiến trúc B của bản 2 được giữ nguyên).

### 1.3 Định nghĩa & viết tắt
| Thuật ngữ | Định nghĩa |
|---|---|
| Ngày (Day) | Một chu kỳ gồm 4 pha: Planning → Simulation → Incident → Summary |
| MAKE IT SO | Nút chốt lệnh của ngày; kết thúc pha Planning; bất đảo |
| Công ty | Thực thể của người chơi; có Uy tín và Doanh thu |
| Đơn hàng (Order) | Yêu cầu vận chuyển: khách, loại hàng, cảng đi → cảng đến, hạn, doanh thu, phạt |
| Tàu (Ship) | Đơn vị điều phối; có Thuyền trưởng (Captain), Speed class, Condition, Status, vị trí |
| Cảng (Port) | Node trên bản đồ |
| Tuyến (Route/Edge) | Cạnh nối hai cảng; có độ dài (route-units) và hazard |
| Lộ trình (Route Option) | Danh sách tuyến **được thiết kế sẵn** (tối đa 2 lựa chọn) giữa cảng đi và cảng đến |
| Lệnh (Command) | Assign (giao đơn) · Reposition (điều tàu) · Repair (sửa) · Hold (mặc định) |
| Sự cố (Incident) | Tình huống xảy ra với tàu đang chạy, người chơi chọn phương án xử lý |
| Outcome | Kết quả quyết định sự cố: Normal / Smart / Slip-up |
| Uy tín (Reputation) | Chỉ số 0–100 quyết định kết cục |
| Doanh thu (Revenue) | Điểm tích lũy, **không dùng để chi tiêu** |
| Hàng (Cargo) | Mỗi đơn có 3 mức: Intact / Scratched / Ruined |
| Cờ (Flag) | Boolean độc lập: `CargoShifted` (theo đơn), `TemporaryRepair` (theo tàu) |
| Manh mối (Clue) | Thông tin hiển thị đủ để người chơi suy luận outcome trước khi chọn |

### 1.4 Tài liệu tham chiếu
- SRS "Aye Aye, Captain... Maybe?" bản 2 (12/11/2026) — nguồn của kiến trúc terminal (mục 3.10 cũ), EventScheduler, GameState/seed, test.
- **Mã nguồn hiện có trong repo là nguồn sự thật cho phần được tái sử dụng.** Nếu SRS và code mâu thuẫn ở phần tái sử dụng, agent phải ghi nhận và báo lại (xem Phụ lục A).
- IEEE 830-1998 (khung cấu trúc).

### 1.5 Tổng quan tài liệu
Mục 2 tổng quan · Mục 3 yêu cầu chức năng · Mục 4 phi chức năng · Mục 5 dữ liệu mẫu · Mục 6 loại trừ · Mục 7 truy vết v2→v3 (tái dùng / thay / mới) · Phụ lục A–B.

---

## 2. Mô tả tổng quan

### 2.1 Góc nhìn sản phẩm
**Giữ nguyên kiến trúc B:** một Scene; máy tính là vật thể world-space — bấm vào máy thì camera zoom vào vùng "màn hình kính", hiệu ứng CRT bật, UI game hiển thị; `Esc` để thoát (CRT tắt, camera lùi ra). Tham chiếu thẩm mỹ: terminal thập niên 1960.

**Thay đổi so với bản 2:** nội dung màn hình không còn 3 trang Briefing/Status/Assignment mà là **3 tab** cố định cùng thanh trạng thái:

```
┌──────────────────────────────────────────────────────────────┐
│ NGÀY 3/8     UY TÍN 58     DOANH THU 340      [PHA: PLANNING]│  ← thanh trên
├──────────────────────────────────────────────────────────────┤
│ [ ĐƠN HÀNG (2) ]  [ ĐỘI TÀU ]  [ SỰ CỐ ]                     │  ← thanh tab (kèm badge)
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                  (nội dung của tab đang chọn)                │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│ log / thoại thuyền trưởng                  [ MAKE IT SO ▶ ]  │  ← thanh dưới
└──────────────────────────────────────────────────────────────┘
```

Sự liên kết với game cũ (easter egg): bốn thuyền trưởng là **Mira, Bob, Dex, Chen** (giữ tính cách); Ngày 1 có đơn "bánh sinh nhật nhiều tầng – dễ vỡ".

### 2.2 Chức năng chính
1. Vòng lặp ngày 4 pha, tối đa 8 ngày (mục 3.1).
2. Khung terminal + 3 tab + điều hướng theo pha (mục 3.2).
3. Tab 1 — nhận/từ chối đơn (mục 3.3).
4. Tab 2 — danh sách tàu + bản đồ + ra lệnh điều phối (mục 3.4).
5. Tab 3 — sự cố của các tàu đang chạy, người chơi chọn phương án (mục 3.5).
6. Tính điểm, tổng kết ngày, 5 đoạn kết, Restart (mục 3.6–3.7).

### 2.3 Người dùng
Người chơi web (itch.io), chỉ dùng chuột (cộng `Esc`), một lần chơi 8–12 phút (tối đa ~15). Không save dài hạn, không multiplayer.

### 2.4 Ràng buộc
- Unity 6000.3, C#, build WebGL, một Scene chính.
- **Mọi màn hình game dựng bằng UI Toolkit trong `#screen` của panel world-space.** Canvas uGUI cũ phải được gỡ/tắt hoàn toàn khi xong (đây cũng là việc "Phase 4" còn dở của bản 2).
- Input System Package (legacy tắt); các asmdef phải khai tham chiếu tường minh.
- Dữ liệu (ship, port, route, order, incident, ending, config) dùng ScriptableObject; **không hard-code số cân bằng**.
- **Logic game là C# thuần (không phụ thuộc UnityEngine)**; UI chỉ hiển thị và gửi "ý định" (intent). UI không tự tính toán kết quả.
- Mọi ngẫu nhiên đi qua `GameState.Rng` với `Seed` → tái lập được.
- Nguyên tắc thiết kế: **mỗi lệnh phải đọc được từ manh mối; không có kết quả ngẫu nhiên "vô lý"** — người chơi suy luận được, kể cả khi vẫn có thể sai.
- Không pathfinding thời gian chạy: lộ trình là **dữ liệu thiết kế sẵn** (có thể sinh bằng công cụ Editor), tối đa 2 lựa chọn mỗi cặp cảng.

### 2.5 Giả định & phụ thuộc (giá trị mặc định đề xuất trong `GameConfig`, chỉnh bằng BalanceProbe)
| Tham số | Mặc định |
|---|---|
| `gameLengthDays` | 8 |
| `startReputation` | 50 |
| `maxActiveOrders` | 4 |
| `newOrdersPerDay` | 2–3 (Ngày 1: 2 đơn cố định) |
| `incidentChancePerMovingShip` | 0.6 |
| `maxIncidentsPerDay` | 2 |
| Speed (route-units/ngày) | Fast 1.5 · Normal 1.0 · Slow 0.7 |
| Outcome Δ tiến độ | Smart +0.5 · Normal 0 · Slip-up −0.7 |
| Điều chỉnh theo phương án (`baseProgressDelta`) | liều/nhanh +0.3 · chờ/chậm −0.3 · trung tính 0 |
| Uy tín giao đúng hạn | +6 (Intact +2 thêm · Scratched 0 · Ruined −8) |
| Uy tín giao trễ | −5, doanh thu ×0.5 |
| Uy tín đơn đã nhận nhưng chưa giao khi hết ván | −8 mỗi đơn |
| Hiệu ứng gõ chữ | phải bỏ qua được (skip) |

---

## 3. Yêu cầu chức năng

Quy ước: mỗi FR có mức ưu tiên và AC. "Hệ thống" = toàn bộ game.

### 3.1 Luồng game & chu kỳ ngày (FR-01xx)

**FR-0101 [M] — Bắt đầu game.** Từ màn hình Start (trong kính), nút Start Game bắt đầu Ngày 1: `currentDay=1`, Uy tín = `startReputation`, Doanh thu = 0, 4 tàu ở cảng khởi đầu theo `ShipDef.startPort`, Condition = Tốt, không cờ nào.
- AC: nhấn Start → Tab ĐƠN HÀNG hiện đúng 2 đơn cố định của Ngày 1, trạng thái khởi đầu đúng.

**FR-0102 [M] — Vòng lặp ngày 4 pha.** `Planning` (người chơi ra lệnh) → `Simulation` (tàu chạy, tự động) → `Incident` (giải quyết sự cố, có thể 0 sự cố) → `Summary` (tổng kết) → ngày +1 hoặc Ending.
- AC: chơi liên tục Ngày 1 → 8 không kẹt ở pha nào; pha không có sự cố thì tự bỏ qua `Incident`.

**FR-0103 [M] — MAKE IT SO là điểm chốt bất đảo.** Khi bấm, các lệnh đã giao (staged) được thực thi, Inbox chưa xử lý bị hết hạn (FR-0304), không thể quay lại sửa lệnh/Inbox của ngày đó.
- AC: sau MAKE IT SO, mọi nút sửa lệnh/Accept/Decline của ngày vừa chốt biến mất hoặc bị vô hiệu.

**FR-0104 [M] — Chỉnh lệnh tự do trước MAKE IT SO.** Trong pha Planning, người chơi có thể gán, đổi, hủy lệnh bất kỳ số lần.
- AC: đổi/hủy lệnh không để lại trạng thái rác (đơn trở lại "chờ giao", tàu trở lại không lệnh).

**FR-0105 [M] — Điều kiện kết thúc.** Ván kết thúc sau Summary của Ngày `gameLengthDays`, hoặc ngay khi Uy tín ≤ 0 (phá sản sớm).
- AC: Uy tín ≤ 0 → chuyển ngay sang Ending E5.

**FR-0106 [S] — Hộp xác nhận trước MAKE IT SO.** Nếu còn đơn đã nhận chưa có tàu, hoặc có lệnh dự kiến trễ hạn, hiện cảnh báo (không chặn; có "Vẫn chốt").
- AC: cảnh báo liệt kê đúng các đơn/tàu liên quan.

### 3.2 Khung terminal & điều hướng tab (FR-02xx)

**FR-0201 [M] — Thanh trên.** Hiển thị Ngày hiện tại/tổng, Uy tín, Doanh thu, tên pha. Cập nhật tức thời khi giá trị đổi.

**FR-0202 [M] — Ba tab.** `ĐƠN HÀNG`, `ĐỘI TÀU`, `SỰ CỐ`; đổi tab bằng chuột. Mỗi tab có badge số: Inbox chưa xử lý · đơn đã nhận chưa có lệnh · sự cố chưa giải quyết.
- AC: badge đúng và biến mất khi hết việc.

**FR-0203 [M] — Khóa tab theo pha.**
| Pha | Tab 1 | Tab 2 | Tab 3 | Nút dưới |
|---|---|---|---|---|
| Planning | tương tác | tương tác | chỉ đọc (log; nếu rỗng: "Chưa có sự cố. Mừng quá sớm?") | MAKE IT SO |
| Simulation | khóa | khóa (hiện tàu chạy) | khóa | ẩn |
| Incident | chỉ đọc | chỉ đọc | **tự chuyển sang**, tương tác | "Tiếp tục" (chỉ bật khi hết sự cố) |
| Summary | overlay tổng kết che toàn màn | | | "Ngày kế" |
- AC: không thể ra lệnh ngoài pha Planning.

**FR-0204 [M] — Hành vi máy.** Bấm máy → zoom contain vào `#screen` → CRT bật → UI game; `Esc` → CRT tắt → camera về vị trí cũ. Thoát/vào lại **không** làm mất trạng thái game.
- AC: thoát giữa pha Incident rồi vào lại, sự cố đang xử lý vẫn còn nguyên.

**FR-0205 [S] — Nút thoát máy trên UI** (ngoài `Esc`).

**FR-0206 [S] — Hiệu ứng gõ chữ** cho thoại/manh mối; click hoặc phím bất kỳ để hiện toàn bộ ngay.

### 3.3 Tab 1 — ĐƠN HÀNG (FR-03xx)

**FR-0301 [M] — Inbox đơn mới.** Đầu mỗi pha Planning, Inbox nhận `newOrdersPerDay` đơn. Mỗi thẻ đơn hiển thị: tên khách (hài), loại hàng (Thường / Dễ vỡ / Lạnh / Khẩn — có thể nhiều thẻ), cảng đi → cảng đến, hạn (Ngày x), doanh thu, mức phạt trễ, **một dòng manh mối**.

**FR-0302 [M] — Accept / Decline.** Accept → đơn vào danh sách "Đơn đã nhận". Decline → loại bỏ, không phạt.
- AC: không đơn nào vừa ở Inbox vừa ở "Đã nhận".

**FR-0303 [M] — Giới hạn đơn đã nhận.** Số đơn đang hoạt động ≤ `maxActiveOrders`; vượt giới hạn thì nút Accept bị vô hiệu kèm lý do.

**FR-0304 [M] — Hết hạn Inbox.** Khi MAKE IT SO, đơn trong Inbox chưa quyết định coi như Decline và ghi vào log.

**FR-0305 [M] — Đơn quá hạn.** Giao sau hạn vẫn được: doanh thu ×0.5 và Uy tín −5. Đơn chưa giao hết ván bị trừ Uy tín theo `GameConfig`.

**FR-0306 [M] — Nguồn đơn.** Ngày 1 cố định (mục 5.4). Ngày 2 → cuối bốc từ `OrderPool` theo trọng số + `spawnCondition`; **không lặp trong một ván**; hết pool thì cho phép lặp (fallback), không bao giờ kẹt game.

**FR-0307 [S] — Tag yêu cầu** (Fragile/Cold/Urgent) được gạch chân để gợi ý tàu phù hợp; **không** nêu thẳng outcome.

### 3.4 Tab 2 — ĐỘI TÀU & ĐIỀU PHỐI (FR-04xx)

**FR-0401 [M] — Bố cục.** Cột danh sách tàu (4 thẻ) + bản đồ + panel lệnh (hiện khi đã chọn tàu).

**FR-0402 [M] — Thẻ tàu.** Hiển thị: tên tàu, thuyền trưởng, Speed class, **Condition** (Tốt / Hao mòn / Hỏng), **Status** (Idle / Moving / Docked), vị trí (tên cảng hoặc "tuyến A→B, x%"), hàng đang chở (đơn + mức Cargo), ETA, cờ đang có kèm cảnh báo, tag tính cách.
- AC: dữ liệu thẻ luôn khớp `GameState`.

**FR-0403 [M] — Bản đồ.** 6 cảng (node), tuyến (edge) có icon hazard (Sương mù, Bão, Hải tặc, Băng, Kẹt cảng, Thông thoáng) và độ dài; tàu hiển thị **đúng vị trí** (tại node hoặc trên cạnh theo tỉ lệ); chọn tàu trên thẻ ↔ highlight trên bản đồ và ngược lại.

**FR-0404 [M] — Lệnh Assign (giao đơn).** Quy trình: chọn tàu Idle **đang ở cảng đi của đơn** → chọn đơn → chọn Route Option (≤2) → xem preview (ETA, so với hạn, hazard dọc đường) → "Giao lệnh" (staged).
- AC: không thể gán tàu không ở cảng đi; không thể gán một đơn cho hai tàu; mỗi tàu tối đa 1 lệnh/ngày.

**FR-0405 [M] — Lệnh Reposition (điều tàu).** Tàu Idle → cảng khác theo `RouteTable` (không chở hàng). Staged như Assign.

**FR-0406 [M] — Hold mặc định.** Tàu không có lệnh đứng yên. Tàu Moving không nhận lệnh mới (tự tiến).

**FR-0407 [M] — Hủy/đổi lệnh staged** (xem FR-0104). Tàu có lệnh staged hiển thị rõ lệnh đó trên thẻ.

**FR-0408 [S] — Lệnh Repair.** Tàu Idle tại cảng, mất 1 ngày để chuyển Condition lên 1 mức. Nếu không làm FR này thì Condition **chỉ có Tốt / Hao mòn** (giới hạn khi hạ cấp ở FR-0506).

**FR-0409 [M] — Giao hàng tự động.** Khi tàu tới cảng đến của đơn đang chở: đơn chuyển `Delivered`, tàu thành Idle; kết quả tính ở Summary.

**FR-0410 [S] — Cảnh báo dự kiến.** Preview đổi sang đỏ nếu ETA > hạn; hiển thị gợi ý mờ về tag không hợp (vd Hàng "Dễ vỡ" + thuyền trưởng "Ẩu"). Cảnh báo **không** tiết lộ outcome.

### 3.5 Tab 3 — SỰ CỐ (FR-05xx)

**FR-0501 [M] — Simulation.** Mỗi tàu Moving tiến `speed` route-units trên lộ trình đã chọn; kiểm tra tới đích (FR-0409). Hiển thị chuyển động ngắn trên bản đồ (có thể rút gọn).

**FR-0502 [M] — Sinh sự cố.** Sau Simulation, `IncidentScheduler` (tái dùng `EventScheduler`) chọn sự cố cho các tàu **còn Moving** (chưa tới đích):
- Xác suất mỗi tàu = `incidentChancePerMovingShip`; tổng ≤ `maxIncidentsPerDay`.
- Lọc theo `spawnCondition`: hazard của cạnh vừa đi qua/đang đi, loại hàng, Condition, cờ.
- Chọn theo trọng số; không lặp trong ván; hết pool → cho phép lặp.
- **Ngày 1: sự cố cố định** (I-01 Sương mù, mục 5.5) cho tàu đầu tiên đang đi qua tuyến Sương mù.
- **Ngày cuối:** `fixedFinale` (I-F) xuất hiện nếu còn ít nhất một tàu Moving có hàng; nếu không thì bỏ qua.
- AC: cùng `Seed` + cùng chuỗi lệnh → cùng chuỗi sự cố.

**FR-0503 [M] — Thẻ sự cố.** Hiển thị: tàu + thuyền trưởng, vị trí, tình huống, **manh mối** (log thuyền trưởng, thời tiết, tình trạng tàu/hàng/cờ), 2–3 phương án, thoại.

**FR-0504 [M] — Ra lệnh xử lý.** Người chơi chọn 1 phương án cho mỗi sự cố (bắt buộc; luôn có phương án "mặc kệ/giữ nguyên" khi hợp lý). Chọn xong → reveal Outcome + lời giải thích + thoại; không undo.

**FR-0505 [M] — Resolver.** Outcome = tra bảng `(tàu/thuyền trưởng, phương án)` của sự cố, có xét thêm điều kiện (cờ, Condition). Không khớp bảng → **Normal**.
- AC: mỗi `IncidentDef` hợp lệ phải khai đủ bảng; Validate báo thiếu.

**FR-0506 [M] — Hệ quả.**
- Δ tiến độ = `baseProgressDelta` của phương án + Δ của Outcome (mặc định mục 2.5); tiến độ không âm.
- Tối đa **1 cờ mỗi sự cố**.
- Mức Cargo **chỉ đổi ở điểm được ghi rõ** trong `IncidentDef` (không tự suy ra).
- Condition chỉ hạ khi ghi rõ trong `IncidentDef` (tối đa tới Hao mòn nếu không làm FR-0408).
- Có thể kèm thay đổi Doanh thu (vd phí qua đường) nhưng Doanh thu không âm.

**FR-0507 [M] — Không softlock.** Mọi trạng thái tàu sau sự cố đều có đường tiếp tục (luôn có thể tiến tiếp hoặc giữ nguyên); hết sự cố → Summary.

**FR-0508 [S] — Nhật ký sự cố** các ngày trước, xem được ở Tab 3 (chỉ đọc).

### 3.6 Tổng kết ngày & chấm điểm (FR-06xx)

**FR-0601 [M] — Summary overlay.** Liệt kê: đơn đã giao (đúng hạn/trễ, mức Cargo), thay đổi Doanh thu, thay đổi Uy tín (từng khoản), đơn còn quá hạn, thay đổi tình trạng tàu/cờ.

**FR-0602 [M] — Công thức điểm** theo `GameConfig` (mặc định mục 2.5). `ScoreCalculator` là hàm thuần, có test.
- AC: giao Intact đúng hạn → Uy tín +8 (6+2); giao Ruined đúng hạn → Uy tín −2 (6−8) và doanh thu 0.

### 3.7 Kết thúc & Restart (FR-07xx)

**FR-0701 [M] — 5 đoạn kết theo Uy tín cuối:**
| Mã | Điều kiện | Tên | Ý chính |
|---|---|---|---|
| E1 | ≥ 85 | Đế chế "Sếp Vĩ Đại" | Mọi tàu hát vang, ít nhất một thuyền trưởng khóc vì cảm động |
| E2 | 65–84 | Công ty có tiếng | Khách quay lại, Dex vẫn nghi ngờ |
| E3 | 40–64 | Sống sót qua tuần | Hòa vốn, Chen "đang tới" |
| E4 | 20–39 | Nợ ngập đầu | Bob đề xuất "bán tàu cho khỏe" |
| E5 | < 20 hoặc ≤ 0 sớm | Phá sản | Cả đội tàu đòi lương bằng... bánh |

**FR-0702 [M] — Màn hình kết thúc** hiển thị đoạn kết, Uy tín, Doanh thu, số đơn đúng hạn/trễ/hỏng.

**FR-0703 [M] — Restart.** Ván mới với `Seed` mới, reset toàn bộ trạng thái (tàu, đơn, cờ, pool đã dùng).

### 3.8 Phong cách hình ảnh (FR-08xx)

**FR-0801 [M] — Terminal retro:** nền tối, chữ xanh/hổ phách, font **JetBrains Mono** (OFL) — **bắt buộc kiểm tra hiển thị đủ dấu tiếng Việt** ở mọi cỡ chữ dùng.
**FR-0802 [S] — Icon tàu/hazard bằng ký tự ASCII hoặc hình vẽ USS đơn giản**, không cần asset ảnh.
**FR-0803 [M] — Hiệu ứng CRT** giữ như bản 2 (mở: vạch nở; tắt: thu dọc → vạch → chấm).

### 3.9 Kiến trúc & dữ liệu (FR-09xx)

**FR-0901 [M] — ScriptableObject:** `GameConfig`, `ShipDef` (kèm captain, speed class, traits, startPort, startCondition), `PortDef`, `RouteDef` (edge: từ, đến, độ dài, hazard), `RouteTable` (route options theo cặp cảng, ≤2), `OrderDef`, `OrderPool`, `IncidentDef`, `IncidentPool` (kèm `fixedFinale`), `EndingDef`.

**FR-0902 [M] — Trạng thái runtime thuần C#:** `GameState` mở rộng từ bản 2 (`Seed`, `Rng`, `Day`, `Phase`, `Reputation`, `Revenue`, `Ships[]`, `Orders[]`, `StagedCommands`, `UsedOrders`, `UsedIncidents`, `NewRun(seed)`).

**FR-0903 [M] — Dịch vụ logic (C# thuần, có test):** `OrderScheduler`, `IncidentScheduler` (từ `EventScheduler`), `CommandValidator`, `SimulationStep`, `IncidentResolver` (từ `DecisionResolver`), `ScoreCalculator`, `EndingEvaluator`.

**FR-0904 [M] — Presenter UI Toolkit** (`OrdersTabView`, `FleetTabView`, `IncidentsTabView`, `SummaryView`, `TopBarView`, `TerminalView` đã có) chỉ render + phát intent; không chứa luật game.

**FR-0905 [M] — Menu Editor idempotent:** `Tools/AyeAye/Build Demo` dựng toàn bộ dữ liệu mẫu mục 5; `Tools/AyeAye/Setup Terminal UI` dựng object Terminal; `Tools/AyeAye/Validate` kiểm tra tham chiếu, bảng outcome đủ, route nối được, không có đơn không thể giao.

---

## 4. Yêu cầu phi chức năng

| Mã | Yêu cầu | Tiêu chí |
|---|---|---|
| NFR-01 | Build WebGL chạy trên itch.io | Build mở được trên Chrome/Firefox; kích thước nén hợp lý (mục tiêu < 40 MB) |
| NFR-02 | Thời lượng | 8–12 phút, tối đa ~15 phút kể cả đọc chữ |
| NFR-03 | Điều khiển | Chỉ chuột + `Esc`; mọi phím qua Input System Package |
| NFR-04 | Hiệu năng | ≥ 30 fps ổn định trên WebGL máy trung bình; panel world-space + CRT không gây khựng |
| NFR-05 | Khả đọc | Chữ rõ ở độ phân giải thấp; tiếng Việt đủ dấu; độ tương phản đủ |
| NFR-06 | Không softlock | Không có tổ hợp lệnh/sự cố nào khiến game kẹt (có test mô phỏng) |
| NFR-07 | Cân bằng | Đo bằng `BalanceProbe` (mô phỏng thuần C#): chính sách ngẫu nhiên đạt ≥ E2 ≤ 10% số ván; chính sách "đọc manh mối" đạt ≥ E2 ≥ 70% |
| NFR-08 | Kiểm thử | EditMode test phủ: `OrderScheduler`, `IncidentScheduler` (seed), `CommandValidator`, `SimulationStep`, `IncidentResolver`, `ScoreCalculator`, `EndingEvaluator`, flow 8 ngày theo seed, BalanceProbe; toàn bộ pass |
| NFR-09 | Tách lớp | Namespace Logic/Data không tham chiếu `UnityEngine.UI`/UI Toolkit |
| NFR-10 | Không lưu dài hạn | Không PlayerPrefs/save; refresh = ván mới |
| NFR-11 | Nguồn gốc asset | Ghi chú nguồn asset/font khi submit itch.io; khai báo AI nếu quy định yêu cầu |

---

## 5. Dữ liệu mẫu (đặc tả nội dung — dùng làm đầu vào cho `Build Demo`)

> Mọi con số là **giá trị khởi đầu**, được phép chỉnh bằng BalanceProbe. Tên/thoại là gợi ý, agent có thể viết thêm cho hài miễn giữ nguyên **logic ghép (tàu × phương án → outcome)**.

### 5.1 Cảng (6)
`Cảng Gốc` (HQ) · `Phố Cảng Vàng` · `Đảo Sương Mù` · `Bến Cá Mập` · `Mỏm Gió` · `Trạm Lạnh Bắc`

### 5.2 Tuyến (7 cạnh, độ dài = route-units)
| Cạnh | Dài | Hazard |
|---|---|---|
| Gốc – Vàng | 1 | Thông thoáng |
| Gốc – Sương Mù | 1 | Sương mù |
| Vàng – Cá Mập | 2 | Hải tặc |
| Sương Mù – Cá Mập | 1 | Sương mù |
| Vàng – Mỏm Gió | 1 | Bão |
| Cá Mập – Trạm Lạnh | 1 | Băng |
| Mỏm Gió – Trạm Lạnh | 2 | Thông thoáng |

Ví dụ hai Route Option Gốc → Trạm Lạnh: (a) Gốc–Sương Mù–Cá Mập–Trạm Lạnh (3 đơn vị, hazard Sương mù + Băng); (b) Gốc–Vàng–Mỏm Gió–Trạm Lạnh (4 đơn vị, Bão nhưng ngắn).

### 5.3 Tàu (4)
| Tàu | Thuyền trưởng | Speed | Tag hiển thị | Cảng đầu |
|---|---|---|---|---|
| CARGO-9 | Bob | Fast | Nhanh · Ẩu · Thích đường tắt | Gốc |
| HAULER-4 | Chen | Slow | Chậm · Thân vững · Bình tĩnh | Gốc |
| FREIGHT-12 | Mira | Normal | Chuẩn sách · Hơi cứng nhắc | Gốc |
| TRANSIT-3 | Dex | Normal | Đa nghi · Kiểm tra kỹ | Vàng |

### 5.4 Đơn hàng
**Ngày 1 (cố định):**
| Đơn | Khách | Hàng | Tuyến | Hạn | Doanh thu | Manh mối |
|---|---|---|---|---|---|---|
| O-01 | Tiệm Bánh Hành Tinh Xa | Dễ vỡ ("Bánh sinh nhật nhiều tầng") | Gốc → Vàng | N3 | 120 | Thùng ghi DO NOT SHAKE / KEEP UPRIGHT / DELIVER BEFORE THE CANDLES |
| O-02 | Cửa hàng Dép Lê Số 1 | Thường | Gốc → Cá Mập (qua Sương Mù) | N4 | 100 | Tuyến Sương Mù ngắn nhưng báo cáo thời tiết ghi "mù như tâm trạng sếp" |

**Pool (bốc từ Ngày 2):**
| Đơn | Khách | Hàng | Tuyến | Hạn | DT | Ghi chú |
|---|---|---|---|---|---|---|
| O-03 | Kem Que Sa Mạc | Lạnh | Gốc → Mỏm Gió | N4 | 90 | tàu chậm vẫn kịp |
| O-04 | Tiệm Gương Vỡ Hạnh Phúc | Dễ vỡ | Sương Mù → Cá Mập | N5 | 110 | gợi ý tàu "vững" |
| O-05 | Pizza Giao Chậm Là Miễn Phí | Khẩn | Gốc → Vàng | N3 | 70 | gợi ý tàu nhanh |
| O-06 | Hội Sưu Tập Đá Cuội | Thường | Vàng → Trạm Lạnh | N7 | 100 | có 2 Route Option |
| O-07 | Đám Cưới Cá Heo | Dễ vỡ + Khẩn | Cá Mập → Gốc | N6 | 160 | khó, thưởng cao |
| O-08 | Bưu Kiện Không Ghi Tên | Thường | Mỏm Gió → Cá Mập | N8 | 130 | Dex: "Đừng mở." |

### 5.5 Sự cố (7 + finale)
Ký hiệu: **[Bob]** = CARGO-9, **[Chen]** = HAULER-4, **[Mira]** = FREIGHT-12, **[Dex]** = TRANSIT-3. "any" = bất kỳ thuyền trưởng. Không khớp bảng = **Normal**.

| ID | Điều kiện sinh | Phương án | Smart | Normal | Slip-up | Hệ quả đặc biệt |
|---|---|---|---|---|---|---|
| **I-01 Sương mù dày** (Ngày 1 cố định) | hazard = Sương mù | A. Giữ tốc độ · B. Giảm tốc dò đường · C. Theo tín hiệu cảng | Mira+C · Chen+B | any+B | Bob+A · Dex+C | Bob+A đặt `CargoShifted` (nếu có hàng). Manh mối: "Mira có sách tín hiệu cảng; Dex ngờ tín hiệu" |
| **I-02 Bão** | hazard = Bão | A. Xuyên bão · B. Neo chờ · C. Vòng tránh | Chen+A · Mira+C | any+B | Bob+A · Dex+C | Slip-up: Condition → Hao mòn. Manh mối: "HAULER-4 đã gia cố thân tàu" |
| **I-03 Hải tặc** | hazard = Hải tặc | A. Trả phí qua đường · B. Phóng nhanh thoát · C. Mặc cả/đánh lừa | Dex+C · Bob+B | any+A | Chen+B · Mira+C | any+A: −20 Doanh thu. Manh mối: "Dex đã nghi từ lúc rời cảng" |
| **I-04 Kẹt cảng** | cạnh vừa đi tới cảng đến/đích trung gian | A. Xếp hàng chờ · B. Nhờ quan hệ chen ngang · C. Chuyển bến phụ | Bob+B · Mira+C | any+A | Dex+B · Chen+C | Manh mối: "Bob quen nửa bến cảng" |
| **I-05 Hàng rung lắc** | Hàng Dễ vỡ | A. Giảm tốc · B. Chằng lại hàng giữa biển · C. Mặc kệ | Mira+B | any+A · Chen+C | Bob+B · (any trừ Chen)+C | Slip-up: Cargo −1 mức |
| **I-06 Máy móc hỏng** | Condition = Hao mòn **hoặc** `TemporaryRepair=true` (trọng số ×3) | A. Sửa chuẩn (mất 1 ngày) · B. Sửa tạm (nhanh) | Mira+A · Bob+B | Chen+A · Bob+A | Dex+B | Bob+B đặt `TemporaryRepair=true`. Thoại Bob: "Nó chạy rồi! ừ thì… hơi rung, và có tiếng lạ nữa." |
| **I-07 Tín hiệu cầu cứu** | bất kỳ | A. Bỏ qua · B. Đi giúp · C. Kiểm tra tín hiệu | Bob+B (nhận quà, tiến thêm) | any+A · Mira+C | Dex+C · Chen+B | Thoại Dex: "Quá trùng hợp. Ai lại mất tín hiệu đúng lúc cần tiệc?" |
| **I-F Cuộc gọi Chủ tịch** (`fixedFinale`, ngày cuối) | còn tàu Moving có hàng | A. Ép hết tốc lực · B. Giữ nhịp · C. Xin gia hạn khách | Mira+C · Bob+A (chỉ khi không có cờ) | Chen+B | Dex+C · Bob+A khi `TemporaryRepair=true` | `CargoShifted=true` + A → Cargo −1 mức |

### 5.6 Kịch bản Ngày 1 (tutorial)
1. Tab ĐƠN HÀNG: hai đơn O-01, O-02; hướng dẫn một dòng cho Accept.
2. Tab ĐỘI TÀU: ba tàu ở Gốc, một ở Vàng; hướng dẫn chọn tàu → đơn → tuyến; preview hiện ETA.
3. MAKE IT SO → Simulation → I-01 xuất hiện (tàu đầu tiên đi qua Sương Mù).
4. Người chơi nhìn thấy manh mối và tự quyết; xem outcome.
5. Summary Ngày 1.

---

## 6. Phạm vi loại trừ (không làm trong jam)
- Điều khiển tàu trực tiếp; di chuyển tự do; pathfinding thời gian chạy; camera tự do (camera chỉ do hệ máy tính điều khiển).
- Tiền tệ để chi tiêu, mua tàu, nâng cấp, nhiên liệu, lương; thủy thủ chết/đổi người; hệ thống thời tiết mô phỏng.
- Hai lệnh cho một tàu trong một ngày; chuyển hàng giữa các tàu; hủy đơn đã nhận.
- Hội thoại phân nhánh dài; AI sinh hội thoại; multiplayer; lưu game dài hạn; animation nhân vật nhiều khung.

---

## 7. Truy vết v2 → v3 (cho agent: tái dùng / thay / mới)

| Thành phần | Quyết định | Ghi chú |
|---|---|---|
| Terminal world-space, `TerminalView`, CRT, zoom, `Esc`, `SetupTerminalUI` | **Tái dùng** | Chỉ đổi nội dung bên trong `#screen` |
| Canvas uGUI cũ + màn hình Briefing/Status/Assignment/ResultOverlay | **Xóa/thay** | Thay bằng UXML 3 tab + Summary |
| `GameState`, `Seed/Rng`, `NewRun` | **Mở rộng** | Thêm Ships/Orders/StagedCommands/Phase |
| `EventScheduler` | **Tái dùng** → `IncidentScheduler` | Thêm lọc hazard/cargo/condition |
| `DecisionResolver` | **Tổng quát hóa** → `IncidentResolver` | Khóa bảng `(captain, option)` |
| `CargoTracker`, flags | **Tái dùng** | Cargo theo từng đơn; `TemporaryRepair` theo tàu |
| `TravelProgress`, `DeliveryEvaluator`, `GameConfig`, `Endings` | **Thay/viết lại** | Đổi sang route-units, Uy tín, 5 ending theo Uy tín |
| `BalanceProbe`, test EditMode, `Validate`, `Build Demo` | **Tái dùng khung, viết lại nội dung** | |
| Mới | **Viết mới** | `OrderScheduler`, `CommandValidator`, `SimulationStep`, `ScoreCalculator`, map view, 3 tab view |

---

## Phụ lục A — Việc đầu tiên của agent: kiểm kê repo
SRS này **không biết** trạng thái chính xác của code hiện tại. Trước khi viết bất kỳ code nào, agent phải:
1. Đọc cấu trúc `Assets/_Project/Scripts/Runtime/{Core, Data, Logic, UI, Presentation}` và `Editor`, các asmdef, scene `Main.unity`.
2. Lập **bảng đối chiếu** với mục 7 (thành phần nào thực sự tồn tại, tên/chữ ký thật, test nào đang pass).
3. Báo các điểm lệch giữa SRS và code, đề xuất cách xử lý, rồi mới triển khai.

Lưu ý kỹ thuật đã biết từ bản 2: `PanelSettings.m_RenderMode = WorldSpace` phải đặt qua `SerializedObject` (field internal ở 6000.3.3f1); ảnh máy vẽ bằng `SpriteRenderer` riêng, panel **không** vẽ ảnh; Input System: legacy tắt; các asmdef cần tham chiếu `Unity.InputSystem`, `Unity.TextMeshPro` (nếu còn dùng).

## Phụ lục B — Điểm mở / giả định đã chọn (agent ghi lại thay đổi vào `ASSUMPTIONS.md`)
1. Số ngày 8, giá trị Uy tín/Doanh thu/ngưỡng ending (mục 2.5, 3.7) là mặc định — chốt bằng BalanceProbe.
2. Lệnh Repair (FR-0408) và nhật ký sự cố (FR-0508) là [S]; nếu bỏ thì áp dụng ràng buộc Condition chỉ Tốt/Hao mòn.
3. Lộ trình sinh bằng công cụ Editor hay viết tay là tùy agent, miễn không pathfinding lúc chạy và mỗi cặp ≤ 2 lựa chọn.
4. Thời lượng thực khi có chữ + hiệu ứng gõ: rút ngắn nếu vượt 12–15 phút (giảm `gameLengthDays` hoặc số sự cố).
5. Tên game "Aye Aye, Boss... Maybe?" là tên tạm.
