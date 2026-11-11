# SRS — Software Requirements Specification
## Aye Aye, Captain... Maybe?

| Mục | Giá trị |
|---|---|
| Tài liệu | SRS dựa trên GDD v0.4 ("Bản chốt cho jam") |
| Ngày lập | 11/11/2026 |
| Sự kiện | Week Sauce · Tháng 11/2026 · Chủ đề "Make It So" |
| Nền tảng | Unity 2D · C# · uGUI · TextMeshPro · WebGL / itch.io |
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
Mục 2 mô tả tổng quan; mục 3 là yêu cầu chức năng chi tiết; mục 4 là yêu cầu phi chức năng; mục 5 là đặc tả nội dung 7 ngày (dữ liệu game); mục 6 là phạm vi loại trừ; mục 7 là ma trận truy vết GDD ↔ SRS; phụ lục ghi trạng thái triển khai hiện tại.

---

## 2. Mô tả tổng quan

### 2.1 Góc nhìn sản phẩm
Game độc lập, một Scene duy nhất, tất cả tương tác qua UI Panel (không có gameplay 3D/tựa trực tiếp). Tham chiếu thẩm mỹ: máy tính/thiết bị điều khiển thập niên 1960 (terminal CRT). Kiện hàng duy nhất là chiếc bánh sinh nhật nhiều tầng ("The Birthday Cake – Fragile"), thùng có 3 dòng cảnh báo: DO NOT SHAKE / KEEP UPRIGHT / DELIVER BEFORE THE CANDLES. Số nến = số tuổi người nhận, lớn một cách vô lý (chất hài).

### 2.2 Chức năng chính
1. Vòng lặp ngày 3 trang (Briefing → Status → Assignment) rồi End Day — lặp tối đa 7 ngày.
2. Hệ thống quyết định: phương án + thủy thủ → 1 trong 3 outcome kèm lời giải thích và thoại.
3. Hệ quả: quãng đường (theo outcome), tình trạng Hàng (chỉ tại điểm ghi rõ), tối đa 1 cờ mỗi quyết định.
4. Thanh tiến độ dưới màn hình cập nhật sau mỗi End Day; đạt 100% trước hoặc vào cuối Ngày 7 = thắng.
5. 5 đoạn kết theo thời hạn và tình trạng Hàng; màn hình kết thúc + nút Restart.

### 2.3 Người dùng
Người chơi web (itch.io), điều khiển bằng chuột, chơi một lần 8–12 phút (tối đa ~15 phút nếu chữ dài), không cần kiến thức trước. Không có save dài hạn, không multiplayer.

### 2.4 Ràng buộc
- Engine Unity 2D, C#, uGUI, TextMeshPro; build WebGL, submit lên itch.io.
- Một Scene chính; chuyển trang bằng UI Panel; ngày/hành trình quản lý bằng dữ liệu trạng thái.
- Dữ liệu quyết định/nhân vật/diễn biến dùng ScriptableObject.
- Nguyên tắc sản xuất đã chốt: **mỗi ngày một quyết định, mỗi quyết định đẩy con tàu và chiếc bánh đến gần đích** (Ngày 4 là ngoại lệ có hai việc nhưng vẫn một lần Confirm).

### 2.5 Giả định & phụ thuộc
- Thang quãng đường prototype: Normal = 1 đơn vị; Smart = 1,5–2; Slip-up = 0,25–0,5; tổng quãng đường chuẩn hóa 100%. Con số chốt sau khi nội dung 7 ngày chạy được và cân bằng (mục "điểm cần chốt" của GDD).
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
- AC: đếm theo bảng mục 5 — mỗi ngày ≥ 2 tổ hợp Smart; mỗi nhân vật có ít nhất 1 ngày hợp và 1 ngày không hợp trong 7 ngày mẫu.

### 3.5 Kết quả & tiến độ (FR-05xx)

**FR-0501 — Ba loại kết quả hiển thị.** Sau End Day, hiển thị outcome + lời giải thích + thoại của nhân vật.
- AC: từng outcome trong 7 ngày mẫu có thoại khớp mục 5 (thoại gợi ý, được phép polish lời nhưng giữ ý).

**FR-0502 — Quãng đường theo outcome.** TravelProgress nhận outcome, trả về distanceDelta: Normal = 1; Smart = 1,5–2; Slip-up = 0,25–0,5 (giá trị prototype, cân bằng sau).
- AC: quãng đường chưa khóa là giá trị cấu hình được, không hard-code trong UI.
- AC: UI không tự tính quãng đường; chỉ hiển thị giá trị từ GameState.

**FR-0503 — Thanh tiến độ.** Luôn nằm cạnh dưới giao diện; thể hiện điểm xuất phát, vị trí tàu, điểm đến và phần trăm hoàn thành; cập nhật sau End Day kèm hiệu ứng tàu tiến lên.
- AC: phần trăm = currentProgress / totalDistance; chỉ nhảy sau End Day, không đổi khi đang chọn phương án.

**FR-0504 — Đến sớm kết thúc ngay.** Đạt 100% trước hoặc vào cuối Ngày 7 → kết thúc thắng ngay lập tức, không chơi tiếp các ngày còn lại, kèm dòng thoại "đến sớm".
- AC: đạt 100% ở ngày < 7 → hiện màn kết thúc ngay; không hiện Briefing ngày kế.

### 3.6 Hàng và cờ hệ quả (FR-06xx)

**FR-0601 — Tình trạng Hàng (3 mức).** `CargoCondition ∈ {Intact, Scratched, Ruined}`, bắt đầu Intact. Chỉ giảm tại các điểm ghi rõ trong bảng ngày (không giảm mỗi ngày). Ảnh hưởng đoạn kết, không ảnh hưởng quãng đường.
- Điểm giảm theo bảng mẫu: Ngày 3 — Bob + C khi `CargoShifted = true` → Hàng giảm 1 mức; Ngày 7 — chọn A khi `CargoShifted = true` → Hàng giảm 1 mức.
- AC: một ngày chỉ giảm tối đa 1 mức; từ Intact không thể nhảy thẳng Ruined trong một quyết định.

**FR-0602 — Hai cờ Boolean độc lập.** `CargoShifted` đặt ở Ngày 2 (Bob xuyên thiêng thạch); `TemporaryRepair` đặt ở Ngày 6 (Bob sửa tạm). Hai cờ có thể cùng tồn tại đến Ngày 7. Mỗi quyết định đặt tối đa 1 cờ.
- AC: cả hai cờ có thể đồng thời true; không có hệ thống thể lực/nghỉ ngơi/tinh thần đi kèm.

**FR-0603 — Hiển thị bánh.** Bánh hiển thị bằng biểu tượng ASCII/pixel 7 tầng trong Status; hư hỏng thể hiện bằng đổi biểu tượng và dòng chữ trên bánh (ví dụ "CHÚC MỪNG SINH NHẬT" mất phần cuối); không cần animation.
- AC: 3 mức tình trạng có 3 biểu hiện phân biệt được; 7 tầng có mặt ở biểu tượng.

**FR-0604 — Cờ tác động Ngày 7.** Khi `TemporaryRepair = true`: phương án A (hạ cánh gắt) bị khóa. Khi `CargoShifted = true` và chọn A: Hàng giảm 1 mức.
- AC: với mọi tổ hợp cờ, Ngày 7 luôn còn ít nhất một phương án khả dụng (GDD mục "điểm cần chốt").

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

- AC: sau mỗi lần chơi kết thúc, đoạn kết hiển thị đúng theo bảng trên; có nút Restart khởi động lại Day 1 với trạng thái ban đầu.

### 3.8 Trình bày & giao diện (FR-08xx)

**FR-0801 — Thẩm mỹ terminal 1960s.** Nền xanh đen/đen than; chữ xanh lá nhạt, vàng hổ phách hoặc trắng ngà; đèn cảnh báo đỏ; font monospace; tiêu đề in hoa; khung kiểu terminal với mã chuyến hàng, ngày hiện tại, trạng thái hệ thống.
- AC: cả 3 trang + thanh tiến độ + màn kết thống nhất một hệ thẩm mỹ.

**FR-0802 — Hiệu ứng gõ chữ.** Có thể dùng hiệu ứng gõ chữ/scanline/glow nhạt/con trỏ nhấp nháy; bắt buộc bỏ qua được hoặc hiện toàn bộ ngay; không lạm dụng.
- AC: người chơi có thể skip hiệu ứng và tương tác ngay.

**FR-0803 — Nhân vật.** Chân dung đơn giản (silhouette hoặc hình khối); phản ứng bằng biểu tượng, đổi biểu cảm, rung khung và câu thoại. Không cần animation nhiều khung.
- AC: mỗi outcome có phản ứng thị giác + thoại của nhân vật liên quan.

**FR-0804 — Nền tàu.** Cầu tàu chỉ là một nền tĩnh tối giản phía sau các màn hình; không vẽ tàu chuyển động liên tục.

### 3.9 Dữ liệu & kiến trúc (FR-09xx)

**FR-0901 — Dữ liệu ScriptableObject.** `DailyBriefingDefinition`, `DecisionDefinition`, `CrewDefinition`; mỗi `DecisionDefinition` chứa danh sách phương án; mỗi phương án chứa danh sách `CrewOutcomeEntry`: `crewId`, `outcome`, `explanation`, `dialogue`, `flagToSet` (nếu có), `cargoDelta` (nếu có), `flagCondition` (ví dụ "chỉ khi CargoShifted = true").
- AC: thêm/sửa một ngày chỉ cần sửa asset, không sửa code resolver.

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

---

## 4. Yêu cầu phi chức năng

| Mã | Yêu cầu | Tiêu chí nghiệm thu |
|---|---|---|
| NFR-01 | Nền tảng: WebGL chạy trên trình duyệt desktop | Build WebGL chạy được; submit itch.io với tag #weeksauce, ghi chủ đề Make It So |
| NFR-02 | Thời lượng: mục tiêu 8–12 phút một ván (tối đa ~15 phút nếu chữ dài) | Đo khi có chữ + hiệu ứng; rút ngắn nếu vượt 12–15 phút |
| NFR-03 | Chữ dễ đọc: font monospace, độ tương phản đủ trên nền tối | Kiểm tra font + độ tương phản trên WebGL build |
| NFR-04 | Tải trang: thời gian tải WebGL chấp nhận được trên itch.io | Kiểm tra thời gian tải khi build sớm (GDD yêu cầu build thử sớm để kiểm font, độ phân giải, chuột) |
| NFR-05 | Công bằng: kết quả không bao giờ cảm giác ngẫu nhiên hay bất công | Mọi outcome có lời giải thích ngắn; mọi Slip-up có manh mối trước Confirm |
| NFR-06 | Tính xác định: cùng một chuỗi quyết định cho cùng kết quả (không RNG trong việc xác định outcome/đoạn kết) | Chạy 2 ván cùng lựa chọn → cùng đoạn kết |
| NFR-07 | Cân bằng nhanh: bảy ngày toàn Normal phải suýt thắng hoặc thua nhẹ; cần vài Smart để thắng thoải mái; chọn ngẫu nhiên không thể thắng | Chạy bảng tính nhanh theo thang mục 2.5 |
| NFR-08 | Kiểm được: các logic cốt (resolver, evaluator, progress, cargo, flow) có test EditMode | Test suite chạy pass trong Unity Test Runner |

---

## 5. Đặc tả nội dung 7 ngày (dữ liệu game)

Bảng dưới là yêu cầu nội dung bắt buộc của MVP. Cách đọc: "Smart: Dex + B" = chọn phương án B, giao cho Dex. Nội dung nằm trong ScriptableObject (FR-0901), resolver không hard-code.

### Ngày 1 — Rời bến (ngày hướng dẫn)
- **Tình huống:** Bến yêu cầu xác nhận niêm phong thùng bánh trước khi rời.
- **Manh mối:** Briefing: dây buộc số 3 của thùng đã được thay vội ở bến. Status: Dex báo "có gì đó không ổn với dây buộc".
- **Phương án:** A. Ký xác nhận luôn · B. Kiểm tra thủ công · C. Chạy quét tự động.
- **Smart:** Dex + B (phát hiện dây buộc lỏng và buộc lại).
- **Normal:** Chen + A; Mira + C.
- **Slip-up:** Bob + B (siết thêm hai vòng cho "chắc", bánh bị ép lõm, mất thời gian tháo ra).
- **Hệ quả:** không đặt cờ, không trừ Hàng.
- **Thoại Slip-up (Bob):** "Chắc chắn là tốt! Tôi đã siết thêm hai vòng nữa!"

### Ngày 2 — Mưa thiêng thạch nhỏ
- **Tình huống:** Tàu đi vào vùng thiêng thạch nhỏ.
- **Manh mối:** Briefing: mật độ thiêng thạch thấp ở rìa bên phải.
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

### Ngày 4 — Hai việc cùng lúc (ngày phá nhịp)
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

### Ngày 7 — Nước rút và hạ cánh
- **Tình huống:** Hạ cánh xuống hành tinh đích trước lúc thổi nến.
- **Phương án:** A. Hạ cánh gắt và nhanh · B. Hạ cánh chậm và êm · C. Theo hướng dẫn của bến.
- **Hệ quả từ cờ:** `TemporaryRepair = true` → A bị khóa (động cơ không chịu nổi). `CargoShifted = true` + chọn A → Hàng giảm 1 mức.
- **Smart:** Mira + C; Bob + A (chỉ khi không có cờ nào).
- **Normal:** Chen + B.
- **Slip-up:** Dex + C (nghi ngờ hướng dẫn của bến và tự đổi đường bay).
- **Ý nghĩa:** người chơi không có cờ nào được thoải mái hơn — phần thưởng cho sự cẩn thận các ngày trước.
- **Thoại Slip-up (Mira):** "Hướng dẫn nói 'hạ cánh nhẹ nhàng'. Tôi đã hạ nhẹ nhàng. Rất nhẹ nhàng. Ba lần."

---

## 6. Phạm vi loại trừ (không làm trong jam)
- Điều khiển tàu trực tiếp; bản đồ không gian tự do; camera di chuyển.
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

## Phụ lục A — Trạng thái triển khai hiện tại (phục vụ phân tích)
Repo đã có bộ khung khớp kiến trúc GDD mục 10: `Assets/_Project/Scripts/Runtime/{Core, Data, Gameplay, UI, Presentation}`; các test EditMode cho DecisionResolver, GameStateFlow, DeliveryEvaluator, TravelProgress, CargoTracker; dữ liệu mẫu `Assets/_Project/Data/{Days, Decisions, Crew, Endings}` (Day1–Day3, Endings asset). Phân tích tiếp theo nên kiểm tra: mức độ khớp của bảng quyết định trong asset với bảng mục 5, giá trị thang quãng đường hiện tại, và độ bao phủ test so với FR-09xx.

## Phụ lục B — Điểm mở cần chốt khi prototype
1. Tổng quãng đường (thang 100% hay số chuẩn hóa) và giá trị cụ thể Normal / Smart / Slip-up.
2. Bảng tính nhanh cân bằng (7 ngày toàn Normal → suýt thắng/thua nhẹ; vài Smart → thắng thoải mái; chọn ngẫu nhiên không thể thắng).
3. Thời lượng thực khi có chữ + hiệu ứng gõ (rút ngắn nếu vượt 12–15 phút).
4. Quy tắc điểm đến: kết thúc Ngày 7 vẫn tính đúng hạn (FR-0701); chỉ thua nếu hết Ngày 7 mà tiến độ dưới 100%.
5. Ngày 7 luôn còn ít nhất một phương án khả dụng trong mọi tổ hợp cờ (FR-0604).
