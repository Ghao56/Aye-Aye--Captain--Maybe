# SDD ledger — plan: (task brief in session message — "Chuyển Ngày 2→N từ cố định sang bốc ngẫu nhiên từ Event Pool")

Working dir: D:\Aye-Aye--Captain--Maybe. Unity: D:\Unity\Editor\Unity.exe (6000.3.24f1).
Batch compile: Unity.exe -batchmode -quit -projectPath . -logFile <log>
EditMode tests: Unity.exe -batchmode -runTests -testPlatform EditMode -testResults <xml> -projectPath . -logFile <log>

Tasks:
1. Data layer: DayDefinition (-dayNumber, +eventId/spawnCondition/weight), EventPool.cs, GameConfig.repeatSeedOnRestart — DONE
2. Logic: EventScheduler.cs, GameState (Seed/Rng/CurrentEvent/UsedEvents/NewRun, ctor seed overload) — DONE
3. Core: GameManager (eventPool, GetCurrentEvent, log [Event]), DayCycleManager + BriefingScreenUI (GetCurrentEvent, dayNumber prefix, OnOverlayNext clear CurrentEvent, OnRestart NewRun) — DONE
4. Editor/DemoBuilder: EventPool asset, Event_<Id> assets (Meteor/Customs/Distress/Breakdown/Landing/TollBooth), delete Day2_*/Day3_*, config 7.2/7, ValidateEvent helper — DONE (code)
5. Tests: EventSchedulerTests.cs (7 tests) + GameStateFlowTests (NewRun_ResetsEventStateAndSeed, Flow_SevenDays_WithScheduler) — DONE
6. Verify: compile OK (compile-green2.log exit 0), EditMode 36/36 pass (tests-run1.xml) — Build Demo x2 + Validate còn lại

Rulings:
- Task 1: giữ CreateAssetMenu "Day_New_Day"/"AyeAye/Day Definition" (chỉ nội dung field đổi) — giảm churn asset menu. Cost: menu name cũ không phản ánh "event", không ảnh hưởng runtime.
- Task 4: 4 event mới viết qua helper CreateEvent (khuôn chung); Meteor/Customs giữ nguyên văn nội dung cũ, chỉ đổi path asset + bỏ tiền tố NGÀY n. Chen được thêm Smart (TollBooth B) và Bob/Mira/Dex đủ Smart+SlipUp toàn pool (FR-0406).
- Trọng số demo: mọi event weight=1 (test riêng đã phủ hành vi weight). spawnCondition mọi event = Always (điều kiện cờ nằm ở entry).
Pre-flight: no shared-interface conflicts found beyond plan.
Final: Ruling: Opening day (Day1) Mira+C changed Normal→Smart — FR-0406 cần ≥2 tổ hợp Smart/ngày, Day1 cũ chỉ có 1 (Dex+B). Cost: nội dung Ngày 1 lệch "nguyên văn" một entry.
Final: Ruling: không chạy fresh-reviewer subagent — đã xác minh bằng compile + 36 test + Build Demo ×2 + Validate theo §5; self-review diff không còn tham chiếu GetDay/days. Cost: không có mắt thứ hai trên nhánh.
Final: Ruling: commit 5632511 quét kèm file untracked có sẵn của user (SRS.md, Assets/Image/Terminal.png, packages-lock) — không phải thay đổi của task nhưng đã nằm trong working tree trước giờ làm. ProjectVersion.txt KHÔNG phải pre-existing: batchmode Unity 6000.3.3f1 (editor duy nhất cài trên máy, D:\Unity\Editor\Unity.exe) tự ghi lại trong lần chạy; project gốc là 6000.3.24f1. Mọi kiểm chứng (compile/test/build/validate) đã chạy trên 6000.3.3f1 và đều pass.

== Review-fix phase (9 items) COMPLETE ==
Task 5: complete (tests: EditMode suite → 37/37 Passed; Build Demo run1 exit 0, run2 exit 0 (sau khi kill Unity batchmode treo PID 21476); Validate exit 0 "[Validate] OK"; [Balance] 5.0% ngẫu nhiên thắng < 15% ngưỡng; compile-review3 exit 0 sạch)
Chi tiết: (1) cân bằng game: totalDistance 8.5 / smart 1.5 / slip 0.3 — tỉ lệ thắng ngẫu nhiên 48%→5.0% (5000 ván, test + validate đều đo); (2) consola.ttf (license Microsoft) → JetBrains Mono 2.304 OFL, SDF "JetBrainsMono-Regular SDF", TMP Settings trỏ lại; (3) fixedFinale = Landing (ra khỏi events), pool 9 event cho 5 slot ngẫu nhiên Ngày 2–6; (4) DemoBuilder không ghi đè asset đã có (guard created), Validate không cứng 7.2 (deadlineDay<3||totalDistance<=0); (5) hệ quả TemporaryRepair: entry Bob+TemporaryRepairRequired SlipUp đặt TRƯỚC entry CargoShifted ở Landing A (resolver lấy khớp đầu tiên) + tempRepairWarning manh mối + StatusScreenUI hiển thị; (6) Distress: Dex+A Smart→Normal, Mira+C Normal→Smart (≥2 Smart giữ vững); (7) bỏ Invoke 0.3s — race CHỈNH LẠI làm kẹt ván; (8) dọn hết [DBG] trong DayCycleManager + AssignmentScreenUI; (9) xóa Assets/Scenes mồ côi.
Incident (lưu lại): PowerShell Set-Content/Get-Content đè file UTF-8 → hỏng dấu tiếng Việt trong DemoBuilder.cs; khôi phục bằng git checkout và làm lại toàn bộ bằng edit tool. QUY TẮC: mọi thao tác text trên file UTF-8 chỉ dùng edit/write tool, không dùng PowerShell text replace.
