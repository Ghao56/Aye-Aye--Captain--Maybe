# SRS — Software Requirements Specification
## Aye Aye, Captain... Maybe?

| Mục | Giá trị |
|---|---|
| Tài liệu | SRS dựa trên GDD v0.4 ("Bản chốt cho jam") |
| Ngày lập | 11/11/2026 · **Bản 2 — cập nhật 12/11/2026: kiến trúc B (máy tính là vật thể world-space), mục 2.1/3.10/Phụ lục A** |
| Sự kiện | Week Sauce · Tháng 11/2026 · Chủ đề "Make It So" |
| Nền tảng | Unity 6000.3 · C# · UI Toolkit (UIDocument world-space) + uGUI/TextMeshPro · Input System Package · WebGL / itch.io |
| Đối tượng đọc | Dev, tester, và các AI/agent phân tích, estimate, dựng backlog |
| Ngôn ngữ | Tiếng Việt (mã yêu cầu FR/NFR giữ tiếng Anh để truy vết máy) |

---

## 1. Giới thiệu

### 1.1 Mục đích tài liệu
Đặc tả đầy đủ và kiểm chứng được các yêu cầu của game "Aye Aye, Captain... Maybe?" cho bản nộp game jam, dựa trên GDD v0.4. Mỗi yêu cầu có mã định danh (FR-xx / NFR-xx) và tiêu chí nghiệm thu (acceptance criteria) để AI/agent khác có thể phân tích, đánh giá độ bao phủ và sinh test case.

### 1.2 Phạm vi sản phẩm
Game quản lý–giải đố hài hước theo ngày: người chơi là thuyền trưởng một tàu giao hàng chở chiếc bánh sinh nhật nhiều tầng đến một sinh vật ngoài hành tinh ở hành tinh xa, trong tối đa 7 ngày. Mỗi ngày: đọc báo cáo → xem tình hình → chọn phương án và giao việc cho 1 trong 4 thủy thủ → End Day → tàu tiến một quãng đường tương ứng. Thắng/thua chỉ theo thời hạn; sắc thái kết thúc theo tình trạng bánh.

### 1.3 Định nghĩa & viết tắt
| Thuật ngữ | Định nghĩa |
|---|---|
| Ngày (Day) | Một chu kỳ chơi gồm Briefing → Status → Assignment → End Day |
| End Day | Điểm chốt ngày: giải quyết quyết định, cập nhật tiến độ, chuyển ngày. Không thể quay lại sửa |
| Outcome | Kết quả một quyết định: Normal / Smart / Slip-up |
| Hàng (Cargo) | Chiếc bánh sinh nhật; 3 mức tình trạng: Intact / Scratched / Ruined |
| Cờ hệ quả (Flag) | Boolean độc lập: `CargoShifted`, `TemporaryRepair` |
| Briefing | Trang 1 trong ngày: mục tiêu, tiến độ, thông báo quan trọng, manh mối |
| Status | Trang 2 trong ngày: tình trạng tàu, bánh, phi hành đoàn, cờ đang có |
| Assignment | Trang 3 trong ngày: chọn phương án, giao thủy thủ, Confirm Decision |
| Manh mối (Clue) | Thông tin trong Briefing/Status đủ để người chơi suy luận kết quả trước khi Confirm |
| Đoạn kết (Ending) | 1 trong 5 kết cục theo đúng/trễ hạn và tình trạng Hàng |

### 1.4 Tài liệu tham chiếu
- `Aye_Aye_Captain_Maybe_GDD_v0_4.docx` — Game Design Document v0.4 (nguồn duy nhất của các yêu cầu nghiệp vụ).
- IEEE 830-1998 (khung cấu trúc tài liệu).

### 1.5 Tổng quan tài liệu
Mục 2 mô tả tổng quan; mục 3 là yêu cầu chức năng chi tiết (3.10 bổ sung kiến trúc B — hệ máy tính vật thể); mục 4 là yêu cầu phi chức năng; mục 5 là đặc tả nội dung ngày và pool event (dữ liệu game); mục 6 là phạm vi loại trừ; mục 7 là ma trận truy vết GDD ↔ SRS; phụ lục ghi trạng thái triển khai hiện tại.

---

## 2. Mô tả tổng quan

### 2.1 Góc nhìn sản phẩm
**Kiến trúc B (duyệt 11/11/2026, hoàn thiện 12/11/2026):** một Scene duy nhất; chiếc máy tính điều khiển tàu là **vật thể world-space** — kéo đi đặt đâu cũng được, bấm vào để vào dùng, `Esc` để ra. UI game vẽ trong vùng "màn hình kính" của máy: vào máy thì camera zoom vào đúng vùng kính, hiệu ứng CRT bật, sau đó giao diện game (Briefing/Status/Assignment…) hiển thị lấp đầy khung nhìn. Tham chiếu thẩm mỹ: máy điều khiển thập niên 1960 (terminal CRT). Kiện hàng duy nhất là chiếc bánh sinh nhật nhiều tầng ("The Birthday Cake – Fragile"), thùng có 3 dòng cảnh báo: DO NOT SHAKE / KEEP UPRIGHT / DELIVER BEFORE THE CANDLES. Số nến = số tuổi người nhận, lớn một cách vô lý (chất hài).

Chi tiết kỹ thuật hệ máy tính: đặc tả tại mục 3.10; trạng thái chuyển tiếp UI (uGUI → UI Toolkit) tại Phụ lục A.

### 2.2 Chức năng chính
1. Vòng lặp ngày 3 trang (Briefing → Status → Assignment) rồi End Day — lặp tối đa 7 ngày.
2. Máy tính điều khiển tàu là vật thể world-space tương tác được: bấm vào máy → camera zoom vào màn hình kính → hiệu ứng CRT bật → UI game; `Esc` → hiệu ứng CRT tắt → camera lùi ra (mục 3.10).
3. Hệ thống quyết định: phương án + thủy thủ → 1 trong 3 outcome kèm lời giải thích và thoại.
4. Hệ quả: quãng đường (theo outcome), tình trạng Hàng (chỉ tại điểm ghi rõ), tối đa 1 cờ mỗi quyết định.
5. Thanh tiến độ dưới màn hình cập nhật sau mỗi End Day; đạt 100% trước hoặc vào cuối Ngày 7 = thắng.
6. 5 đoạn kết theo thời hạn và tình trạng Hàng; màn hình kết thúc + nút Restart.

### 2.3 Người dùng
Người chơi web (itch.io), điều khiển bằng chuột, chơi một lần 8–12 phút (tối đa ~15 phút nếu chữ dài), không cần kiến thức trước. Không có save dài hạn, không multiplayer.

### 2.4 Ràng buộc
- Engine Unity 6000.3, C#; build WebGL, submit lên itch.io.
- Input System Package (legacy Input Manager tắt) — mọi phím qua `UnityEngine.InputSystem`; các asmdef (`AyeAye.Runtime`, `AyeAye.Editor`, `AyeAye.Tests.EditMode`) phải khai tham chiếu tường minh (ví dụ `Unity.InputSystem`, `Unity.TextMeshPro`).
- Một Scene chính; UI dùng hai hệ song song trong giai đoạn chuyển tiếp: panel world-space UI Toolkit cho máy tính, uGUI/TextMeshPro cho các màn hình game (Phụ lục A).
- Dữ liệu quyết định/nhân vật/diễn biến dùng ScriptableObject.
- Nguyên tắc sản xuất đã chốt: **mỗi ngày một quyết định, mỗi quyết định đẩy con tàu và chiếc bánh đến gần đích** (Ngày 4 là ngoại lệ có hai việc nhưng vẫn một lần Confirm — hiện ngoài phạm vi triển khai, xem mục 5).
- Kiến trúc ngày đã chốt: **Ngày 1 cố định (ngày hướng dẫn); Ngày 2 → deadlineDay bốc ngẫu nhiên từ Event Pool; ngày cuối dùng `fixedFinale` nếu được gán.** Không lặp event trong một ván; hết pool → cho phép lặp (fallback), không bao giờ kẹt game.

### 2.5 Giả định & phụ thuộc
- Thang quãng đường **ĐÃ CHỐT** (NFR-07): `totalDistance` = 8.5; Normal = 1.0; Smart = 1.5; Slip-up = 0.3; `deadlineDay` = 7. Con số sống trong `GameConfig` asset, không hard-code.
- Hiệu ứng gõ chữ phải bỏ qua được (skip) hoặc hiện toàn bộ ngay.
- Asset nguồn phải ghi chú khi submit itch.io; khai báo AI nếu quy định yêu cầu.

---

## 3. Yêu cầu chức năng

Quy ước: mỗi FR có tiêu chí nghiệm thu (AC). "Hệ thống" = toàn bộ game.

### 3.1 Luồng game & chu kỳ ngày (FR-01xx)

**FR-0101 — Bắt đầu game.** Từ màn hình đầu, nút Start Game bắt đầu Ngày 1 ở trạng thái ban đầu: `currentDay = 1`, tiến độ 0%, Hàng = Intact, hai cờ = false.
- AC: nhấn Start Game → hiện trang Briefing của Ngày 1 với đúng trạng thái khởi đầu.

**FR-0102 — Vòng lặp ngày.** Hệ thống thực thi chuỗi Briefing → Status → Assignment (Confirm Decision) → End Day cho mỗi ngày, sau đó chuyển sang ngày +1 hoặc kết thúc game theo mục 3.7.
- AC: không thể đến Assignment mà bỏ qua Briefing/Status; không thể qua End Day mà chưa Confirm.
- AC: chơi liên tục từ Day 1 đến Day 7 không bị kẹt ở bất kỳ trang nào.

**FR-0103 — End Day là điểm chốt bất đảo.** Sau khi nhấn End Day, quyết định của ngày đó được giải quyết và không thể quay lại sửa; không có undo cho ngày đã kết thúc.
- AC: sau End Day, mọi nút quay lại trang trước/phương án của ngày vừa chốt đều không tồn tại hoặc bị vô hiệu.

**FR-0104 — Chỉnh hiện trạng giữa các trang.** Trong cùng một ngày, trước End Day, người chơi có thể điều chỉnh lựa chọn trên trang Assignment.
- AC: đổi phương án/thủy thủ trước Confirm không tạo lỗi; kết quả giải quyết theo lựa chọn cuối cùng.

### 3.2 Trang Briefing (FR-02xx)

**FR-0201 — Nội dung Briefing.** Mỗi ngày hiển thị: số ngày thứ mấy; điểm xuất phát/đích; số ngày còn lại; tiến độ hiện tại; thông báo quan trọng; manh mối của ngày.
- AC: đủ 6 nhóm thông tin trên cho từng ngày 1–7 (nội dung cụ thể tại mục 5).

**FR-0202 — Nút Continue.** Briefing chuyển sang Status khi nhấn Continue.
- AC: nhấn Continue → hiện trang Status; không có hành động nào khác bắt buộc.

### 3.3 Trang Status (FR-03xx)

**FR-0301 — Nội dung Status.** Hiển thị: tình trạng tàu và bánh; báo cáo phi hành đoàn; sự kiện/cơ hội; cờ hệ quả đang có (dưới dạng đèn báo).
- AC: khi `CargoShifted = true` hoặc `TemporaryRepair = true`, Status hiển thị đèn báo tương ứng từ ngày cờ được đặt trở đi.
- AC: tình trạng bánh hiển thị theo 3 mức (xem FR-0603).

**FR-0302 — Nút Continue.** Status chuyển sang Assignment khi nhấn Continue.
- AC: nhấn Continue → hiện trang Assignment.

### 3.4 Trang Assignment & quyết định (FR-04xx)

**FR-0401 — Chọn phương án và thủy thủ.** Trang hiển thị danh sách phương án của ngày; người chơi chọn 1 phương án và 1 thủy thủ phụ trách.
- Ngoại lệ Ngày 4 (FR-0405): chọn hai thủy thủ khác nhau cho hai việc X, Y; hai người còn lại nghỉ; vẫn một lần Confirm.
- AC (ngày thường): không Confirm được khi thiếu phương án hoặc thủy thủ.
- AC (Ngày 4): không Confirm được khi hai việc trùng người hoặc thiếu một việc.

**FR-0402 — Cảnh báo dự kiến.** Trước khi Confirm, trang hiển thị cảnh báo dự kiến cho tổ hợp đang chọn, dựa trên manh mối đã cho — không tiết lộ trực tiếp outcome.
- AC: mọi tổ hợp có khả năng dẫn tới Slip-up đều có manh mối hiển thị trước đó ở Briefing hoặc Status (nguyên tắc: manh mối phải đủ để suy luận).
- AC: cảnh báo không ghi rõ "sẽ Slip-up" (chỉ gợi ý theo tính cách/tình huống).

**FR-0403 — Confirm Decision.** Nhấn Confirm quyết định tổ hợp (phương án + thủy thủ) cho ngày.
- AC: sau Confirm hiển thị giao diện sẵn sàng End Day (hoặc kết quả+thoại và End Day, theo thiết kế trang).

**FR-0404 — Xác định outcome.** DecisionResolver nhận phương án + thủy thủ (+ GameState), tra bảng outcomes của ngày; trả về một trong: Normal / Smart / Slip-up, kèm: lời giải thích ngắn, thoại, cờ cần đặt (nếu có), cargoDelta (nếu có).
- Mặc định: tổ hợp không có entry riêng → Normal.
- Đặc tả từng tổ hợp của 7 ngày tại mục 5; đây là dữ liệu, không phải logic cứng.
- AC: mỗi tổ hợp trong bảng ngày cho đúng outcome đúng như bảng mục 5.
- AC: outcome không bao giờ "ngẫu nhiên cảm giác bất công": luôn kèm lời giải thích ngắn.

**FR-0405 — Quy tắc riêng Ngày 4 (hai việc).** Smart nếu cả hai người đều hợp; Slip-up nếu có ít nhất một người không hợp; còn lại Normal.
- AC: cấu hình đúng trong bảng Ngày 4 mục 5; một lần Confirm duy nhất.

**FR-0406 — Không có thủy thủ "luôn đúng".** Bảng ngày phải đảm bảo: mỗi ngày có ít nhất 2 cách đạt Smart; không có thủy thủ nào là lựa chọn tối ưu mọi tình huống; Dex đúng ở Ngày 1, sai ở Ngày 5 (người chơi phải tự đọc manh mối).
- ⚠️ Khoảng hở dữ liệu đã biết (cần xử lý trước khi nghiệm thu AC): Ngày 1 trong mục 5 hiện chỉ có 1 tổ hợp Smart (Dex + B) — cần thêm Smart thứ hai vào asset hoặc nới AC; 5 event hài (mục 5) chưa có bảng outcome trong tài liệu (dữ liệu nằm trong asset `Assets/_Project/Data`) — cần bổ sung đặc tả để "đếm theo bảng" kiểm được.
- AC: đếm theo bảng mục 5 — mỗi ngày ≥ 2 tổ hợp Smart; mỗi nhân vật có ít nhất 1 ngày hợp và 1 ngày không hợp trong pool mẫu.

**FR-0407 — Bốc event từ Event Pool.** Từ Ngày 2 đến ngày cuối trừ một (`deadlineDay − 1`), hệ thống bốc ngẫu nhiên một event từ pool (ngày cuối dùng `fixedFinale` — xem cuối danh sách):
- Mỗi event có `eventId`, `weight` (≥1) và `spawnCondition` (điều kiện cờ). `EventScheduler` lọc event đã dùng / không thỏa cờ, bốc theo trọng số.
- Định danh theo **seed** (xem NFR-06): cùng seed + cùng chuỗi lựa chọn → cùng chuỗi event. RNG (System.Random từ seed) CHỈ dùng để bốc event; tuyệt đối không dùng trong DecisionResolver / DeliveryEvaluator / TravelProgress.
- Event được bốc **đúng 1 lần mỗi ngày và cache**: quay lại Briefing ↔ Status ↔ Assignment trong cùng ngày KHÔNG bốc lại. Event được dùng bị loại khỏi pool của ván.
- Pool cạn hoặc không event nào thỏa cờ → fallback cho phép lặp event; không bao giờ trả null làm kẹt game.
- Restart mặc định dùng seed mới (Environment.TickCount); `GameConfig.repeatSeedOnRestart = true` → dùng lại seed cũ.
- Ngày cuối: nếu `EventPool.fixedFinale` được gán → luôn dùng event này; null → bốc như các ngày khác.
- AC: hai ván cùng seed bốc ra cùng chuỗi event; hai ván khác seed có thể khác chuỗi; trong một ván không lặp event khi pool còn; chơi trọn ván không kẹt ở trang nào.

### 3.5 Kết quả & tiến độ (FR-05xx)

**FR-0501 — Ba loại kết quả hiển thị.** Sau End Day, hiển thị outcome + lời giải thích + thoại của nhân vật.
- AC: từng outcome trong 7 ngày mẫu có thoại khớp mục 5 (thoại gợi ý, được phép polish lời nhưng giữ ý).

**FR-0502 — Quãng đường theo outcome.** TravelProgress nhận outcome, trả về distanceDelta từ `GameConfig`: Normal = 1.0; Smart = 1.5; Slip-up = 0.3; `totalDistance` = 8.5 (giá trị đã chốt, NFR-07).
- AC: quãng đường chưa khóa là giá trị cấu hình được, không hard-code trong UI.
- AC: UI không tự tính quãng đường; chỉ hiển thị giá trị từ GameState.

**FR-0503 — Thanh tiến độ.** Luôn nằm cạnh dưới giao diện; thể hiện điểm xuất phát, vị trí tàu, điểm đến và phần trăm hoàn thành; cập nhật sau End Day kèm hiệu ứng tàu tiến lên.
- AC: phần trăm = currentProgress / totalDistance; chỉ nhảy sau End Day, không đổi khi đang chọn phương án.

**FR-0504 — Đến sớm kết thúc ngay.** Đạt 100% trước hoặc vào cuối Ngày 7 → kết thúc thắng ngay lập tức, không chơi tiếp các ngày còn lại, kèm dòng thoại "đến sớm".
- AC: đạt 100% ở ngày < 7 → hiện màn kết thúc ngay; không hiện Briefing ngày kế.

### 3.6 Hàng và cờ hệ quả (FR-06xx)

**FR-0601 — Tình trạng Hàng (3 mức).** `CargoCondition ∈ {Intact, Scratched, Ruined}`, bắt đầu Intact. Chỉ giảm tại các điểm ghi rõ trong bảng ngày (không giảm mỗi ngày). Ảnh hưởng đoạn kết, không ảnh hưởng quãng đường.
- Điểm giảm theo bảng mẫu: event **Trạm hải quan** — Bob + C khi `CargoShifted = true` → Hàng giảm 1 mức; event **Hạ cánh** (`fixedFinale`) — chọn A khi `CargoShifted = true` → Hàng giảm 1 mức.
- AC: một ngày chỉ giảm tối đa 1 mức; từ Intact không thể nhảy thẳng Ruined trong một quyết định.

**FR-0602 — Hai cờ Boolean độc lập.** `CargoShifted` đặt bởi event **Mưa thiên thạch** (Bob + B xuyên thẳng); `TemporaryRepair` đặt bởi event **Hỏng hóc kỹ thuật** (Bob + B sửa tạm). Vì ngày 2–6 bốc ngẫu nhiên từ pool (FR-0407), cờ gắn với **event**, không gắn với số ngày — có thể đặt sớm hơn hoặc muộn hơn tùy ván. Hai cờ có thể cùng tồn tại đến ngày cuối. Mỗi quyết định đặt tối đa 1 cờ.
- AC: cả hai cờ có thể đồng thời true; không có hệ thống thể lực/nghỉ ngơi/tinh thần đi kèm.

**FR-0603 — Hiển thị bánh.** Bánh hiển thị bằng biểu tượng ASCII/pixel 7 tầng trong Status; hư hỏng thể hiện bằng đổi biểu tượng và dòng chữ trên bánh (ví dụ "CHÚC MỪNG SINH NHẬT" mất phần cuối); không cần animation.
- AC: 3 mức tình trạng có 3 biểu hiện phân biệt được; 7 tầng có mặt ở biểu tượng.

**FR-0604 — Cờ tác động event hạ cánh (`fixedFinale`).** Khi `CargoShifted = true` và chọn A: Hàng giảm 1 mức. Khi `TemporaryRepair = true` và chọn A: kết quả Slip-up (vá tạm rung rời khi hạ gắt) kèm cảnh báo hiển thị ở Status (`tempRepairWarning`).
- Quyết định đã chốt: luật "khóa phương án A" KHÔNG khả thi trên data model hiện tại (chưa hỗ trợ khóa phương án theo cờ); thay bằng entry hệ quả Slip-up + manh mối cảnh báo trước đó.
- AC: với mọi tổ hợp cờ, chọn A luôn cho kết quả có lời giải thích rõ ràng; Hàng tối đa giảm 1 mức mỗi quyết định.

### 3.7 Thắng/thua và đoạn kết (FR-07xx)

**FR-0701 — Thắng/thua chỉ theo thời hạn.** Đúng hạn: thanh tiến độ đạt 100% trước hoặc vào cuối Ngày 7 (kết thúc Ngày 7 vẫn tính đúng hạn). Trễ hạn: kết thúc Ngày 7 mà tiến độ dưới 100%.
- AC: chỉ DeliveryEvaluator quyết định Victory/Defeat — nơi duy nhất.

**FR-0702 — Năm đoạn kết.** Chọn theo thời hạn × tình trạng Hàng:

| Kết thúc | Điều kiện | Mô tả |
|---|---|---|
| Perfect Delivery | Đúng hạn, Hàng Intact | Bánh nguyên vẹn, người nhận thổi nến thành công |
| Minor Damage | Đúng hạn, Hàng Scratched | Bánh trầy hoặc lệch chữ; người nhận vẫn vui |
| Disaster Delivery | Đúng hạn, Hàng Ruined | Bánh nát; họ vẫn ăn bằng thìa và cố tỏ ra lịch sự |
| Late but Intact | Trễ hạn, Hàng Intact | Qua giờ thổi nến nhưng bánh hoàn hảo |
| Late Delivery | Trễ hạn, Hàng Scratched hoặc Ruined | Giao sau Ngày 7; chỉ còn lời xin lỗi |

- AC: sau mỗi lần chơi kết thúc, đoạn kết hiển thị đúng theo bảng trên; có nút Restart khởi động lại Day 1 với trạng thái ban đầu và seed mới (hoặc seed cũ nếu `repeatSeedOnRestart`).

### 3.8 Trình bày & giao diện (FR-08xx)

**FR-0801 — Thẩm mỹ terminal 1960s.** Nền xanh đen/đen than; chữ xanh lá nhạt, vàng hổ phách hoặc trắng ngà; đèn cảnh báo đỏ; font monospace; tiêu đề in hoa; khung kiểu terminal với mã chuyến hàng, ngày hiện tại, trạng thái hệ thống.
- AC: cả 3 trang + thanh tiến độ + màn kết thống nhất một hệ thẩm mỹ.

**FR-0802 — Hiệu ứng gõ chữ.** Có thể dùng hiệu ứng gõ chữ/scanline/glow nhạt/con trỏ nhấp nháy; bắt buộc bỏ qua được hoặc hiện toàn bộ ngay; không lạm dụng.
- AC: người chơi có thể skip hiệu ứng và tương tác ngay.

**FR-0803 — Nhân vật.** Chân dung đơn giản (silhouette hoặc hình khối); phản ứng bằng biểu tượng, đổi biểu cảm, rung khung và câu thoại. Không cần animation nhiều khung.
- AC: mỗi outcome có phản ứng thị giác + thoại của nhân vật liên quan.

**FR-0804 — Nền tàu.** Cầu tàu chỉ là một nền tĩnh tối giản phía sau các màn hình; không vẽ tàu chuyển động liên tục.

### 3.9 Dữ liệu & kiến trúc (FR-09xx)

**FR-0901 — Dữ liệu ScriptableObject.** `DayDefinition` (event: `eventId`, `title`, `briefingText`, `statusText` có token `{CARGO}`, `cargoShiftedWarning`, `tempRepairWarning`, `spawnCondition`, `weight`, `decision`), `DecisionDefinition`, `CrewDefinition`, `EventPool` (`openingDay`, `fixedFinale`, `events`), `GameConfig` (giá trị cân bằng, `deadlineDay`, `repeatSeedOnRestart`); mỗi `DecisionDefinition` chứa danh sách phương án; mỗi phương án chứa danh sách `CrewOutcomeEntry`: `crewId`, `condition`, `outcome`, `explanation`, `quote`, `setFlag`, `cargoDelta`.
- AC: thêm/sửa một event chỉ cần sửa asset, không sửa code resolver; pool không đủ event chỉ gây cảnh báo, không kẹt game.

**FR-0902 — Enum chuẩn.** `DecisionOutcome { Normal, Smart, SlipUp }`, `ScreenStep { Briefing, Status, Assignment }`, `CargoCondition { Intact, Scratched, Ruined }`.

**FR-0903 — Phân trách nhiệm component.**

| Component | Trách nhiệm |
|---|---|
| GameManager | Khởi tạo game, giữ trạng thái tổng, điều phối thắng/thua, restart |
| GameState | `currentDay`, `currentProgress`, `totalDistance`, `deadline`, `cargoCondition`, `cargoShifted`, `temporaryRepair`, lựa chọn trong ngày |
| DayCycleManager | Điều khiển Briefing → Status → Assignment → End Day → ngày tiếp theo |
| ScreenFlowController | Bật/tắt Panel, kiểm soát thứ tự màn hình |
| DecisionResolver | Nhận phương án + thủy thủ + GameState, tra `CrewOutcomeEntry`; trả outcome, lời giải thích, cờ, cargoDelta |
| CargoTracker | Áp dụng cargoDelta và cờ; trả tình trạng Hàng hiện tại |
| TravelProgress | Nhận outcome, trả distanceDelta; cập nhật tỷ lệ hành trình |
| DeliveryEvaluator | Nơi duy nhất quyết định Victory/Defeat và chọn đoạn kết |
| ProgressBarUI / CakeStatusUI | Hiển thị tiến độ và tình trạng bánh |

- AC: UI không chứa logic tính quãng đường hoặc thắng/thua.
- AC: đủ các test EditMode cho: DecisionResolver, GameStateFlow, DeliveryEvaluator, TravelProgress, CargoTracker.

**FR-0904 — Luồng kỹ thuật.** Start Game → Begin Day → Screen 1 Briefing → Screen 2 Status → Screen 3 Assign → Confirm Decision → DecisionResolver (Normal/Smart/Slip-up + flags, cargoDelta) → End Day → TravelProgress áp distance; CargoTracker áp cargo/flags → Progress ≥ 100%? Victory (đoạn kết theo cargo) : Day ≥ 7? Defeat (Late) : advance day và lặp.

### 3.10 Máy tính điều khiển tàu — vật thể world-space (FR-10xx, kiến trúc B)

Đặc tả hành vi camera + hiệu ứng + tương tác. Implement trong `TerminalView` trên GameObject `Terminal` (scene Main). Đơn vị world; camera ortho, nền đen.

**FR-1001 — Máy là vật thế world-space, camera đứng yên lúc chờ.** GameObject `Terminal` gồm: `UIDocument` (PanelSettings `TerminalPanelSettings`, renderMode WorldSpace, `worldSpaceSizeMode` Fixed = 19.2×12.8 unit, sân khấu 1920×1280 px), `BoxCollider` trigger 19.2×12.8×0.5, `PanelInputConfiguration` (raycast camera → collider → sự kiện pointer vào UXML), `TerminalView`, và con `ImagePreview` (SpriteRenderer vẽ ảnh máy từ `TerminalPreview.png`, PPU 80). Người chơi kéo object tới vị trí nào camera cũng giữ nguyên lúc Play — máy chỉ là một vật thể trong cảnh.
- AC: Play mà không bấm gì → camera đúng transform đặt trong scene, không bám/đổi vị trí theo máy.
- AC: kéo máy tới vị trí khác → panel UI và ảnh máy đi theo object.

**FR-1002 — Vào máy bằng chuột.** Bấm trúng máy (raycast qua collider) → camera lerp 0.6s (`ZoomSeconds`, smoothstep) dời + thu vào đúng tâm vùng kính `#screen`, chế độ "contain": **orthoSize = max(halfH, halfW / aspect) × Zoom Out Factor** (halfW/halfH lấy từ layout `#screen` quy ra world unit). Hai núm chỉnh trong Inspector trên `TerminalView`: `Zoom Out Factor` (0.3–4; mặc định 1 = vừa khít kính, >1 lùi ra xa) và `Zoom Offset` (Vector2, world unit — lệch tâm zoom; mặc định (0,0)). Cả hai áp dụng cho khung giữ sau khi vào (`KeepScreenFitted`, giữ khi đổi tỉ lệ cửa sổ). Vào lại máy lần 2 chạy lại đầy đủ chuỗi zoom + hiệu ứng mở màn.
- AC: sau zoom, vùng kính chiếm đúng khung nhìn theo hai núm trên; đổi kích thước cửa sổ khung không méo.
- AC: bấm trong lúc chưa sẵn sàng hoặc bấm lần nữa trong lúc đang zoom không gây lỗi trạng thái.

**FR-1003 — Hiệu ứng CRT bật trước khi hiện UI.** Sau zoom (tổng 0.5s, `PowerOnSeconds`): vạch sáng giãn ngang toàn kính trong **40% thời lượng đầu**, rồi **nở đối xứng từ giữa phủ trọn kính** trong 60% còn lại (top = 50% − height/2, phủ cả nửa trên), đồng thời lớp đen mờ dần; xong mới bật UI game. Canvas uGUI của UI game bị tắt lúc chờ bấm và chỉ được bật tại thời điểm này (bản vá chuyển tiếp — Phụ lục A).
- AC: UI game không hiện trước khi hiệu ứng mở màn kết thúc.
- AC: nửa trên kính được phủ bởi vạch sáng (không chỉ nở một chiều xuống dưới).

**FR-1004 — Thoát máy bằng `Esc` với hiệu ứng CRT tắt.** Trong máy, nhấn `Esc` (Input System, `Keyboard.current.escapeKey`; phím được phát qua Input System nên hoạt động cả WebGL, cần smoke-test trên build): UI game tắt ngay → hiệu ứng tắt 0.5s không khoảng chết: màn thu dọc về vạch sáng giữa (0→60% thời lượng), vạch thu ngang về chấm (60→100%) → tắt lớp hiệu ứng **ngay trước khi** camera lùi → camera lerp 0.6s về đúng transform trước khi vào máy → trạng thái chờ, bấm máy lại vào được (máy ở trạng thái "tắt", kính về hiển thị thường).
- AC: camera trả về đúng vị trí + ortho size trước khi zoom (giá trị chụp tại lúc bấm vào).
- AC: không còn lớp hiệu ứng nào đọng lại sau khi camera về chỗ (kính về trạng thái thường).
- AC: không có nhịp đứng hình giữa hai pha thu màn.

**FR-1005 — Gizmo vùng zoom.** Chọn object `Terminal` trong Scene view: khung vàng wireframe = khung camera sau khi zoom. Edit mode vẽ theo 4 hằng khớp `.screen` trong `Terminal.uss` (shortcut: đổi USS phải đổi hằng — `TerminalView.cs`); Play mode vẽ theo layout thật của panel. Khung có cả `Zoom Out Factor` + `Zoom Offset` và theo vị trí/scale object.
- AC: gizmo hiện cả ở edit mode (không cần Play) và cập nhật khi kéo object.

**FR-1006 — Nội dung panel theo sân khấu cố định.** Panel world-space cố định 1920×1280 px (1px = 0.01 unit, tức sân khấu = 19.2×12.8 unit). Vùng kính `#screen` trên sân khấu: **x = 375 px, y = 106.25 px (từ mép trên), w = 1218.75 px, h = 862.5 px** — chiếm cùng tỉ lệ vị trí trên ảnh máy gốc 1536×1024 (3:2) là 300/85/975×690. Ảnh máy (1536×1024 @ PPU 80 = 19.2×12.8 unit) khớp đúng panel. Toàn bộ số đo nằm trong `Terminal.uss` (R6). Chữ hiệu ứng gõ CRT thuộc pha sau (Phase 4, Phụ lục A).

---

## 4. Yêu cầu phi chức năng

| Mã | Yêu cầu | Tiêu chí nghiệm thu |
|---|---|---|
| NFR-01 | Nền tảng: WebGL chạy trên trình duyệt desktop | Build WebGL chạy được; submit itch.io với tag #weeksauce, ghi chủ đề Make It So |
| NFR-02 | Thời lượng: mục tiêu 8–12 phút một ván (tối đa ~15 phút nếu chữ dài) | Đo khi có chữ + hiệu ứng; rút ngắn nếu vượt 12–15 phút |
| NFR-03 | Chữ dễ đọc: font monospace, độ tương phản đủ trên nền tối | Kiểm tra font + độ tương phản trên WebGL build |
| NFR-04 | Tải trang: thời gian tải WebGL chấp nhận được trên itch.io | Kiểm tra thời gian tải khi build sớm (GDD yêu cầu build thử sớm để kiểm font, độ phân giải, chuột) |
| NFR-05 | Công bằng: kết quả không bao giờ cảm giác ngẫu nhiên hay bất công | Mọi outcome có lời giải thích ngắn; mọi Slip-up có manh mối trước Confirm |
| NFR-06 | Tính xác định theo seed: cùng seed + cùng chuỗi lựa chọn → cùng chuỗi event + cùng đoạn kết (không RNG trong việc xác định outcome/đoạn kết; RNG chỉ dùng bốc event) | Chạy 2 ván cùng seed và cùng lựa chọn → cùng chuỗi event và cùng đoạn kết (test EditMode) |
| NFR-07 | Cân bằng nhanh: bảy ngày toàn Normal phải suýt thắng hoặc thua nhẹ; cần vài Smart để thắng thoải mái; chọn ngẫu nhiên không thể thắng | Đã chốt giá trị: totalDistance = 8.5; Normal = 1.0; Smart = 1.5; Slip-up = 0.3; deadlineDay = 7. Đo bằng mô phỏng: chơi ngẫu nhiên thắng ~5% (5000 ván, ngưỡng <15%, test `RandomPlayBalanceTests` + bước Validate) |
| NFR-08 | Kiểm được: các logic cốt (resolver, evaluator, progress, cargo, flow) có test EditMode | Test suite chạy pass trong Unity Test Runner |

---

## 5. Đặc tả nội dung 7 ngày (dữ liệu game)

Bảng dưới là yêu cầu nội dung bắt buộc của MVP. Cách đọc: "Smart: Dex + B" = chọn phương án B, giao cho Dex. Nội dung nằm trong ScriptableObject (FR-0901), resolver không hard-code. **Chú ý**: bảng dưới đặc tả theo event gốc của GDD (4 event có cờ/Hàng + Ngày 1 hướng dẫn + Ngày 7 finale); 5 event hài bổ sung (TollBooth, SpaceTraffic, RadioBlast, SpaceSnail, StardustFog) không đặt cờ, không hạ Hàng — bảng outcome của chúng chỉ có đầy đủ trong asset `Assets/_Project/Data/Events`, cần chuyển vào bảng này để kiểm AC được.

**Cấu trúc pool đã chốt:** Ngày 1 cố định (`openingDay`); Ngày 2 đến ngày cuối trừ một bốc ngẫu nhiên từ pool (FR-0407); ngày cuối dùng `fixedFinale` = event Hạ cánh. Pool hiện có 9 event cho **4 slot ngẫu nhiên** (Ngày 4 hai việc ngoài phạm vi triển khai): Meteor, Customs, Distress, Breakdown, TollBooth, SpaceTraffic, RadioBlast, SpaceSnail, StardustFog. Năm event sau (TollBooth, SpaceTraffic, RadioBlast, SpaceSnail, StardustFog) là event gốc mới tông hài, không đặt cờ, không hạ Hàng — bổ sung ngoài GDD để pool đủ lớn cho FR-0406 (mỗi nhân vật có ≥1 event hợp và ≥1 event không hợp). Mỗi event tự đủ manh mối trong briefing/status của chính nó (NFR-05); không event nào giả định event khác đã xuất hiện trước đó.

### Ngày 1 — Rời bến (ngày hướng dẫn)
- **Tình huống:** Bến yêu cầu xác nhận niêm phong thùng bánh trước khi rời.
- **Manh mối:** Briefing: dây buộc số 3 của thùng đã được thay vội ở bến. Status: Dex báo "có gì đó không ổn với dây buộc".
- **Phương án:** A. Ký xác nhận luôn · B. Kiểm tra thủ công · C. Chạy quét tự động.
- **Smart:** Dex + B (phát hiện dây buộc lỏng và buộc lại).
- **Normal:** Chen + A; Mira + C.
- **Slip-up:** Bob + B (siết thêm hai vòng cho "chắc", bánh bị ép lõm, mất thời gian tháo ra).
- **Hệ quả:** không đặt cờ, không trừ Hàng.
- **Thoại Slip-up (Bob):** "Chắc chắn là tốt! Tôi đã siết thêm hai vòng nữa!"

### Ngày 2 — Mưa thiên thạch nhỏ
- **Tình huống:** Tàu đi vào vùng thiên thạch nhỏ.
- **Manh mối:** Briefing: mật độ thiên thạch thấp ở rìa bên phải.
- **Phương án:** A. Vòng theo rìa phải (chậm, an toàn) · B. Xuyên thẳng (nhanh) · C. Tắt động cơ, trôi qua.
- **Smart:** Mira + A (đúng theo manh mối); Bob + B (nhanh, nhưng xem Hệ quả).
- **Normal:** Chen + C.
- **Slip-up:** Dex + B (đổi hướng ba lần vì tin thiêng thạch "nhắm vào tàu").
- **Hệ quả:** Bob + B đặt `CargoShifted = true`.
- **Thoại (Bob):** "Nhanh hơn mà! Chiếc bánh chỉ… nghiêng một chút thôi."

### Ngày 3 — Trạm hải quan
- **Tình huống:** Hải quan hỏi về hàng hóa; người chơi chọn câu lệnh nói với đội.
- **Manh mối:** Status: Mira báo "tôi sẽ làm đúng từng chữ lệnh". Briefing: nhắc công thức bánh là bí mật thương mại.
- **Phương án:** A. "Trả lời trung thực mọi câu hỏi." · B. "Khai báo đủ giấy tờ." · C. "Trả lời ngắn gọn."
- **Smart:** Mira + B; Bob + C (chỉ khi `CargoShifted = false` — hải quan quý sự nhiệt tình của anh).
- **Normal:** Chen + bất kỳ.
- **Slip-up:** Mira + A (kể cả "một lượng đường không hợp pháp ở ba hệ sao"); Dex + bất kỳ (từ chối khai báo vì nghi hải quan); Bob + C khi `CargoShifted = true` (làm rơi thùng khi "giúp bê").
- **Hệ quả:** Bob + C khi `CargoShifted = true` → Hàng giảm 1 mức.
- **Thoại Slip-up (Mira):** "Ngài bảo trả lời trung thực. Tôi đã trả lời trung thực."

### Ngày 4 — Hai việc cùng lúc (ngày phá nhịp) — NGOÀI PHẠM VI TRIỂN KHAI
> FR-0405 cần hệ thống phân công đôi riêng (2 người cùng 1 lần Confirm), chưa có trong data model hiện tại. Giữ đặc tả dưới làm yêu cầu tương lai; demo hiện tại không có ngày này.
- **Tình huống:** Máy lọc khí báo động và đường đi bị lệch nhẹ. Hai việc: X. Sửa máy lọc khí · Y. Chỉnh hướng tàu.
- **Phân công:** chọn hai thủy thủ khác nhau cho X và Y; hai người còn lại nghỉ; một lần Confirm.
- **Hợp:** Mira–X (đọc sách hướng dẫn); Bob–Y (nhanh, mạnh tay); Dex–Y (kiểm tra hướng kỹ).
- **Không hợp:** Bob–X (tháo luôn bộ lọc "cho nhanh"); Chen–Y (quá chậm khi gấp).
- **Quy tắc:** Smart nếu cả hai đều hợp; Slip-up nếu ít nhất một người không hợp; còn lại Normal.
- **Hệ quả:** không đặt cờ. Ngày đầu tiên người chơi phải từ chối ai đó.
- **Thoại (Chen, không được giao việc):** "Tuyệt. Tôi sẽ… ủng hộ từ xa."

### Ngày 5 — Tín hiệu cầu cứu lạ
- **Tình huống:** Một tàu lạ phát tín hiệu cầu cứu. Dex cho rằng đây là bẫy.
- **Manh mối:** Thông số tín hiệu khớp một chuyến tàu đã đăng ký và mất liên lạc; lời nhắc nhở đến việc họ đang trên đường dự một bữa tiệc sinh nhật.
- **Phương án:** A. Bỏ qua · B. Đi giúp · C. Kiểm tra tín hiệu trước rồi mới quyết định.
- **Smart:** Bob + B (tàu lạ tặng nhiên liệu và lời chúc, quãng đường dài hơn).
- **Normal:** Bất kỳ + A; Mira + C.
- **Slip-up:** Dex + C (kiểm tra mãi, cuối cùng vẫn nghi ngờ); Chen + B (đến quá chậm).
- **Hệ quả:** không đặt cờ. Lời cảnh báo của Dex đúng ở Ngày 1, sai ở Ngày 5.
- **Thoại (Dex):** "Quá trùng hợp. Ai lại mất tín hiệu đúng lúc cần tiệc?"

### Ngày 6 — Hỏng hóc kỹ thuật
- **Tình huống:** Một bộ phận của hệ thống đẩy hỏng và cần xử lý.
- **Manh mối:** Status: sách hướng dẫn nói "sửa chuẩn mất một ngày"; Bob báo "tôi có cách nhanh hơn".
- **Phương án:** A. Sửa chuẩn (chậm) · B. Sửa tạm (nhanh).
- **Smart:** Mira + A; Bob + B (nhanh, nhưng xem Hệ quả).
- **Normal:** Chen + A; Bob + A.
- **Slip-up:** Dex + B (lắp nhầm vì kiểm tra quá lâu).
- **Hệ quả:** Bob + B đặt `TemporaryRepair = true`.
- **Thoại (Bob):** "Nó chạy rồi! ừ thì… hơi rung, và có tiếng lạ nữa."

### Ngày 7 — Nước rút và hạ cánh (fixedFinale, luôn xuất hiện ở ngày cuối)
- **Tình huống:** Hạ cánh xuống hành tinh đích trước lúc thổi nến.
- **Phương án:** A. Hạ cánh gắt và nhanh · B. Hạ cánh chậm và êm · C. Theo hướng dẫn của bến.
- **Hệ quả từ cờ:** `CargoShifted = true` + chọn A → Hàng giảm 1 mức. `TemporaryRepair = true` + chọn A → Slip-up (vá tạm rung rời khi hạ gắt; cảnh báo hiển thị ở Status từ ngày đặt cờ). Luật "khóa A" của GDD không khả thi trên data model hiện tại — xem FR-0604.
- **Smart:** Mira + C; Bob + A (chỉ khi không có cờ nào).
- **Normal:** Chen + B.
- **Slip-up:** Dex + C (nghi ngờ hướng dẫn của bến và tự đổi đường bay); Bob + A khi `TemporaryRepair = true`.
- **Ý nghĩa:** người chơi không có cờ nào được thoải mái hơn — phần thưởng cho sự cẩn thận các ngày trước.
- **Thoại Slip-up (Bob):** "Nó vẫn chạy mà! ...ừ thì, nửa chặng sau nó nghỉ ngơi." (thoại Mira giữ làm flavor: "Hướng dẫn nói 'hạ cánh nhẹ nhàng'. Tôi đã hạ nhẹ nhàng. Rất nhẹ nhàng. Ba lần.")

---

## 6. Phạm vi loại trừ (không làm trong jam)
- Điều khiển tàu trực tiếp; bản đồ không gian tự do; camera tự do do người chơi điều khiển (camera chỉ bị hệ máy tính điều khiển theo FR-10xx).
- Animation nhân vật nhiều khung; vật lý; chiến đấu thời gian thực.
- Hệ thống thể lực, nghỉ ngơi, tinh thần; mất thủy thủ; nhiên liệu, tiền, nâng cấp, sửa tàu phức tạp.
- Đối thoại phân nhánh quá nhiều; AI sinh hội thoại; multiplayer; lưu game dài hạn.

---

## 7. Ma trận truy vết GDD v0.4 ↔ SRS

| Mục GDD | Nội dung | Yêu cầu SRS |
|---|---|---|
| 0. Thay đổi so với 0.3 | Chốt tên, kiện hàng, lõi gameplay, trạng thái tối thiểu | 1.2, 2.1, 3.6 |
| 1. Tổng quan | Thể loại, engine, thời lượng, pitch | 1.2, 2.1, 2.3, NFR-02 |
| 2. Trải nghiệm cốt lõi | Chu kỳ + 5 nguyên tắc thiết kế | 3.1–3.4, NFR-05, NFR-07 |
| 3. Phong cách hình ảnh | Terminal retro, màu, font, bánh ASCII | 3.8 |
| 4. Cấu trúc một ngày | Ba trang + End Day chốt | 3.1–3.4 |
| 5. Ba kết quả & thanh tiến trình | Outcome, thang quãng đường, progress bar | 3.5, 2.5 |
| 6. Phi hành đoàn | 4 nhân vật + quy tắc không liệt kê | FR-0406, mục 5 |
| 7. Kiện hàng & trạng thái | 3 dòng cảnh báo, 3 mức Hàng, 2 cờ | 3.6 |
| 8. Bảy ngày mẫu | Tình huống, phương án, Smart/Normal/Slip-up, hệ quả, thoại | mục 5 |
| 9. Thắng/thua & đoạn kết | 5 đoạn kết, đúng/trễ hạn | 3.7 |
| 10. Kiến trúc Unity 2D | Components, data model, luồng kỹ thuật | 3.9 |
| 11. Quy ước triển khai | ScriptableObject, enum, UI không tính toán | FR-0901–0903, NFR-08 |
| 12. Phạm vi MVP | Bắt buộc + không làm | mục 3 toàn bộ + mục 6 |
| 13. Kế hoạch 7 ngày | Kế hoạch phát triển | (phạm vi tài liệu SRS — không đặc tả yêu cầu) |
| 14. Điểm cần chốt | Thang quãng đường, cân bằng, thời lượng | NFR-02, NFR-07, 2.5 |
| 15. Checklist itch.io | Build, ảnh, mô tả, tag, nguồn asset | NFR-01, 2.5 |
| 16. Quyết định đã chốt | Bảng quyết định thiết kế | toàn tài liệu |

---

## Phụ lục A — Trạng thái triển khai hiện tại (phục vụ phân tích) — cập nhật 12/11/2026

**Chạy project (dành cho người nhận):** Unity **6000.3.3f1**; mở scene `Assets/_Project/Scenes/Main.unity`. Nếu scene trống/thiếu object: chạy menu `Tools/AyeAye/Build Demo` (dựng dữ liệu) rồi `Tools/AyeAye/Setup Terminal UI` (dựng object Terminal) — cả hai idempotent. Test: Window → General → Test Runner → EditMode (37/37 pass).

**Kiến trúc B (máy tính world-space) đã triển khai:**
- `Terminal` GameObject: UIDocument world-space (19.2×12.8, sân khấu 1920×1280, PanelSettings `m_RenderMode=WorldSpace` đặt qua SerializedObject do field internal ở 6000.3.3f1), BoxCollider + PanelInputConfiguration (raycast input), SpriteRenderer `ImagePreview` vẽ ảnh máy (panel KHÔNG vẽ ảnh, tránh chồng đôi).
- `TerminalView`: camera đứng yên lúc chờ; bấm máy → zoom contain vào `#screen` với `Zoom Out Factor` (0.3–4) + `Zoom Offset` (Vector2); hiệu ứng CRT bật (vạch nở đối xứng) → bật Canvas uGUI cũ → UI game; `Esc` → hiệu ứng tắt (thu dọc → vạch → chấm, không nhịp chết) → gỡ lớp hiệu ứng → camera lerp về transform trước zoom; gizmo khung vàng vùng zoom (edit mode theo hằng khớp USS, play mode theo layout thật).
- `SetupTerminalUI` (menu `Tools/AyeAye/Setup Terminal UI`): dựng idempotent toàn bộ object Terminal + PanelSettings + preview; tắt Canvas uGUI cũ lúc setup.
- Input System Package (legacy tắt); `AyeAye.Runtime.asmdef` đã tham chiếu `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`.

**Chuyển tiếp UI (chưa xong — Phase 4):** các màn hình game (Briefing/Status/Assignment/ResultOverlay/End + header/progress/cake) vẫn là **uGUI trên Canvas bị tắt**; `TerminalView` bật Canvas ngay sau hiệu ứng mở màn — UI overlay fullscreen, nhìn như nằm trên kính vì camera đã zoom cho kính lấp đầy khung. Việc còn lại: chuyển các màn hình sang UXML trong `#screen` của panel, sau đó bỏ đoạn bật Canvas trong `FinishIntro`.

**Kiến trúc game core (đã xong):** `Assets/_Project/Scripts/Runtime/{Core, Data, Logic, UI, Presentation}` + `Editor`. `EventScheduler` (bốc theo weight + spawnCondition, pure C#), `GameState` có `Seed`/`Rng`/`CurrentEvent`/`UsedEvents`/`NewRun(seed)`, `GameManager.GetCurrentEvent()` cache-once, `EventPool` SO với `fixedFinale` = Hạ cánh. Dữ liệu mẫu `Assets/_Project/Data/{Events, Decisions, Config, Crew, Endings}`: `EventPool.asset` + 9 event (mỗi event có asset day + decision riêng), dựng tự động bằng `Tools/AyeAye/Build Demo` (idempotent). Test EditMode: 37/37 pass (EventScheduler, GameStateFlow + flow 7 ngày theo seed, DecisionResolver, CargoTracker, DeliveryEvaluator, TravelProgress, RandomPlayBalance). Font: JetBrains Mono (OFL).

**Giá trị cân bằng đang chạy:** `deadlineDay=7, totalDistance=8.5, normal=1.0, smart=1.5, slipUp=0.3` (asset demo do `DemoBuilder` tạo; default trong `GameConfig.cs` là bản prototype 3 ngày/3.2 — không dùng lúc runtime).

**Chưa triển khai:** FR-0101 (màn hình Start), FR-0402 (cảnh báo dự kiến ở Assignment), FR-0405 (Ngày 4 hai việc), FR-0603 (bánh 7 tầng), FR-1003 phần chữ gõ trong kính (Phase 4), nút thoát máy trên UI game (hiện chỉ `Esc`).

## Phụ lục B — Điểm mở cần chốt khi prototype
1. ~~Tổng quãng đường và giá trị cụ thể Normal / Smart / Slip-up~~ — **ĐÃ CHỐT**: 8.5 / 1.0 / 1.5 / 0.3; chơi ngẫu nhiên thắng ~5% (NFR-07).
2. ~~Bảng tính nhanh cân bằng~~ — **ĐÃ CHỐT**: đo bằng `BalanceProbe` (mô phỏng pure C#) trong test + Validate.
3. Thời lượng thực khi có chữ + hiệu ứng gõ (rút ngắn nếu vượt 12–15 phút).
4. ~~Quy tắc điểm đến: kết thúc Ngày 7 vẫn tính đúng hạn (FR-0701)~~ — **ĐÃ CHỐT** và có test.
5. ~~Ngày 7 luôn còn ít nhất một phương án khả dụng trong mọi tổ hợp cờ~~ — **ĐÃ CHỐT** (FR-0604 dạng hệ quả, không khóa phương án; có entry + test phủ).
