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
