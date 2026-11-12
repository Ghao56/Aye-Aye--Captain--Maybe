# SDD ledger — plan: D:\AGENT_PROMPT.md

Plan: AGENT_PROMPT.md — 6 bước (0 kiểm kê → 6 cân bằng/WebGL). Spec: D:\SRS_v3_Aye_Aye_Boss.md.
Repo: D:\Aye-Aye--Captain--Maybe, nhánh `feature/uitoolkit-terminal` (không phải main, không cần worktree).

Ruling: làm thẳng trên nhánh feature/uitoolkit-terminal — user đã approve toàn bộ flow bằng "MAKE IT SO"; worktree thừa với repo Unity (Library không theo worktree).
Ruling: `M SRS.md` trong working tree là thay đổi của user (v2 SRS) — KHÔNG đụng, KHÔNG commit, KHÔNG revert.
Pre-flight: Bước 0 tiêu thụ bảng §7 của SRS; Bước 1–6 nối tiếp, mỗi bước dùng sản phẩm bước trước. Bước 1 tiêu thụ dữ liệu mẫu §5 (input cho Build Demo mới) — phải đọc §5 trước khi viết SO.
Note: TDD skill sẽ load trước Bước 1 (bước code đầu tiên); Bước 0 chỉ đọc, không có test.
Note: NFR-09 đúng chữ = "Logic/Data không tham chiếu UnityEngine.UI/UI Toolkit" — SO dùng UnityEngine core là hợp lệ; repo hiện tại đạt (Logic thuần C#).

Ruling (user addenda sau Bước 0): (1) CrewOutcomeEntry mở rộng thêm progressDelta + shipConditionDelta + revenueDelta, và DecisionOption có baseProgressDelta riêng (cộng với Δ của Outcome) — thiếu thì bảng §5.5 dựng không được; (2) lookup Canvas uGUI trong TerminalView phải null-safe, không lỗi/không bật gì lạ; dọn code chết đó khi DoD "no old uGUI" chốt (Bước 4); (3) dictionary cờ khóa theo ID tường minh (OrderId, ShipId) — cờ không lẫn giữa các đơn; test seed 8 ngày ổn định.
Ruling: SRS.md trong repo vẫn là v2 (bản user sửa, chưa commit) — KHÔNG đụng; spec ràng buộc của agent là D:\SRS_v3_Aye_Aye_Boss.md (file riêng, giữ nguyên tên); báo user trong report cuối để user tự quyết thay bản repo.

Ruling: lệnh batch test (đã xác minh): `D:\Unity\Editor\Unity.exe -batchmode -runTests -testPlatform EditMode -testResults <đường dẫn file xml tuyệt đối> -projectPath D:\Aye-Aye--Captain--Maybe -logFile <log>` — KHÔNG dùng `-quit` (với -quit, runner bị hủy im lặng, exit 0 nhưng không chạy test nào và không có XML). Unity của user có thể đang mở project → batch sẽ exit ngay (Temp\UnityLockfile); phải hỏi user đóng trước.
Ruling: Bước 1 xóa sớm (nhu cầu compile): v2 logic (EventScheduler, DecisionResolver, CargoTracker, TravelProgress, DeliveryEvaluator, ResolvedDecision, BalanceProbe), v2 Data SO (DayDefinition, DecisionDefinition, EventPool, CrewOutcomeEntry, DecisionOption), v2 Core (GameManager, DayCycleManager, ScreenFlowController), 8 v2 uGUI screen scripts; DemoBuilder rút gọn còn dựng font/TMP (phần dựng dữ liệu mẫu v3 bổ sung ở bước sau); 7 v2 test file xóa (thay bằng tương đương v3). Scene Main.unity sẽ có missing scripts tạm thời — chấp nhận, Bước 2 rewires.
Ruling: TDD theo chu kỳ dịch vụ (RED = lỗi compile do thiếu service, GREEN = toàn bộ suite); mỗi chu kỳ chạy batch 2 lần (RED, GREEN) và commit riêng.

Task 0 (Bước 0 — kiểm kê repo): DONE (báo cáo đã gửi user; bảng đối chiếu trong report, không lệch chặn tiến trình).
Task 1 (Bước 1 / chu kỳ 1 — nền tảng + GameStateBuilder): DONE (RED: CS0103 GameStateBuilder không tồn tại; GREEN: 3/3 GameStateBuilderTests pass, aa-c1-green.xml; commit pending). Files: GameEnums.cs, GameConfig.cs, PortDef.cs, RouteDef.cs, RouteTable.cs, ShipDef.cs, OrderDef.cs, OrderPool.cs, IncidentDef.cs, IncidentPool.cs, RuntimeState.cs, GameState.cs, FlagEvaluator.cs, GameStateBuilder.cs; xóa v2 theo ruling.
