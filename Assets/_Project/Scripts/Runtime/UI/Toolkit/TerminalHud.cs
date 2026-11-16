using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AyeAye
{
    // FR-0201..0203: khung UI Toolkit — thanh trên, 3 tab + khóa theo pha (PhaseGate), nút dưới, Start/Restart.
    // Bước 2 chỉ dựng khung: nội dung tab là placeholder; MAKE IT SO chạy ngày bằng BalanceProbe (tự chơi thay người).
    // shortcut: ngày chạy tự động, tab trả về dữ liệu thật ở Bước 3-5; lúc đó bỏ loop này.
    [RequireComponent(typeof(UIDocument))]
    public partial class TerminalHud : MonoBehaviour
    {
        static readonly string[] TabNames = { "ĐƠN HÀNG", "ĐỘI TÀU", "SỰ CỐ" };

        UIDocument document;
        TerminalView machine;

        VisualElement game, startScreen;
        Label topDay, topLate, topDelivered, topRevenue, topPhase, startTitle, startHint;
        Button[] tabs = new Button[3];
        VisualElement[] panels = new VisualElement[3];
        Button bottomButton, restartButton;
        Label[] panelHints = new Label[3];

        bool bound;
        GameState state;
        SimWorld world;                 // dữ liệu tĩnh của ván cho Simulation.Advance
        float clockAcc;                 // dồn dt thật → chạy Advance theo bước cố định (chỉnh 3)
        int lastRefreshKey = int.MinValue;
        bool focusPaused;               // mất focus / tab ẩn / Application pause → đồng hồ dừng (G2)
        [Tooltip("DEBUG trong editor: bỏ qua tạm dừng khi mất focus (để xem UI khi không giữ cửa sổ).")]
        public bool ignoreFocusPause = false;
        Label firstOrderLabel;          // G7: đếm ngược "đơn đầu tiên sau ~Ns" (đơn đầu không còn có sẵn)
        float countdownAcc;             // cập nhật hạn đơn ~10 Hz bằng cách sửa text tại chỗ
        // (đơn, label, tiền tố, hậu tố) — dựng lại mỗi Refresh; sửa text tại chỗ, không dựng lại panel
        readonly List<(OrderState order, Label label, string prefix, string suffix)> countLabels
            = new List<(OrderState, Label, string, string)>();

        // G5: nhãn "tới sau Ns" của tàu Roaming — cập 10 Hz tại chỗ cùng nhịp UpdateCountdowns.
        readonly List<(ShipState ship, Label label, string prefix, string suffix)> shipEtaLabels
            = new List<(ShipState, Label, string, string)>();

        // Dữ liệu mẫu §5 (nguồn duy nhất DemoData, khớp TestWorld + Build Demo).
        GameConfig config;
        OrderPool pool;
        IncidentPool incidents;
        List<CrewDefinition> crews;
        List<ShipDef> shipDefs;
        EndingDefinitions endings;
        Dictionary<string, string> portNames;
        Dictionary<string, PortDef> portDefs;   // G3: tọa độ bản đồ
        RouteTable routeTable;                  // G3: tra tuyến cho panel lệnh
        List<RouteDef> mapRouteDefs;            // G3: các cạnh vẽ trên bản đồ
        int lastPendingIncidents;               // G6.3b: phát hiện sự cố mới (0→>0) để tự mở tab SỰ CỐ

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            machine = GetComponent<TerminalView>();
            Application.focusChanged += OnFocusChanged; // G2: mất focus cửa sổ → dừng đồng hồ
        }

        void OnDisable()
        {
            Application.focusChanged -= OnFocusChanged;
        }

        // G2: tạm dừng khi mất focus / tab ẩn / app pause; quay lại thì dt bị clamp nên không nhảy đồng hồ.
        void OnFocusChanged(bool focus) => focusPaused = !focus;

        void OnApplicationFocus(bool hasFocus) => focusPaused = !hasFocus;

        void OnApplicationPause(bool paused) { if (paused) focusPaused = true; } // mở lại qua focusChanged

        void Update()
        {
            if (!bound) { TryBind(); if (!bound) return; }
            if (machine != null && game != null)
                game.EnableInClassList("on", machine.IsMachineOn);
            // Bỏ màn BẮT ĐẦU (boss 14/11): máy bật là ván chạy ngay.
            if (machine != null && machine.IsMachineOn && state == null)
                StartNewRun();
            if (state != null && machine != null && machine.IsMachineOn && (ignoreFocusPause || !focusPaused))
            {
                RunClock();
                int inboxCount = 0;
                foreach (var o in state.Orders) if (o.Status == OrderStatus.InInbox) inboxCount++;
                int key = state.LateCount * 1000 + state.DeliveredCount * 100
                    + (state.PendingIncidents.Count > 0 ? 10 : 0) + (state.IsFinished ? 1 : 0)
                    + inboxCount; // đơn mới vào hộp thư / hết hạn cũng cần vẽ lại
                // G5: trạng thái bất kỳ tàu đổi (rời cảng / cập cảng) → dựng lại thẻ + panel lệnh.
                foreach (var s in state.Ships)
                {
                    key = key * 31 + (int)s.Status;
                    key = key * 31 + (s.PortId != null ? s.PortId.GetHashCode() : 0);
                    key = key * 31 + (s.Route != null && s.Route.ToPort != null ? s.Route.ToPort.GetHashCode() : 0);
                }
                // Bug 15/11: key chỉ GHI SAU khi Refresh() chạy trọn — Refresh ném exception thì
                // key chưa lưu, frame sau dựng lại ngay → UI tự hồi phục thay vì đóng băng vĩnh viễn.
                if (key != lastRefreshKey) { Refresh(); lastRefreshKey = key; }
                UpdateCountdowns(); // hạn đơn ~10 Hz — sửa text tại chỗ, không dựng lại panel
            }
            if (state != null)
            {
                UpdateTopPhase(); // G6.3b: text topbar cập MỖI frame — kể cả khi đồng hồ dừng (mất focus/sự cố)
                UpdateFleetDots(); // chấm tàu theo state mỗi frame (panel dựng lại chỉ khi key đổi)
            }
        }

        // Dòng pha topbar — gọi mỗi frame vì đây là CÁCH DUY NHẤT người chơi biết vì sao đồng hồ
        // dừng (bug report 15/11 "giao đơn thứ 3 game đứng": trước đây text này chỉ đổi khi key
        // Refresh đổi, người chơi không thấy trạng thái TẠM DỪNG và tưởng game treo).
        void UpdateTopPhase()
        {
            if (topPhase == null) return;
            if (focusPaused && !ignoreFocusPause) topPhase.text = "TẠM DỪNG (mất focus)";
            else if (state.IsFinished) topPhase.text = "KẾT THÚC";
            else if (!state.CanAdvance) topPhase.text = "TẠM DỪNG (sự cố chờ)";
            else topPhase.text = "ĐANG CHẠY";
        }

        // G1 shim: dồn dt thật rồi chạy Simulation.Advance theo bước simTickSeconds (giới hạn tick mỗi frame).
        // Tạm dừng khi máy tắt, mất focus, sự cố chờ hoặc kết thúc (CanAdvance == false).
        void RunClock()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.5f); // clamp: quay lại từ tab ẩn không nhảy đồng hồ
            clockAcc += dt;
            int guard = 0;
            while (clockAcc >= config.simTickSeconds && state.CanAdvance && guard++ < 60)
            {
                Simulation.Advance(world, state, config.simTickSeconds);
                clockAcc -= config.simTickSeconds;
            }
            if (!state.CanAdvance) clockAcc = 0f;
        }

        void TryBind()
        {
            if (document == null || bound) return;
            var root = document.rootVisualElement;
            if (root == null || root.childCount == 0) return;

            game = root.Q("game");
            startScreen = root.Q("start-screen");
            topDay = root.Q<Label>("top-day");
            topLate = root.Q<Label>("top-late");
            topDelivered = root.Q<Label>("top-delivered");
            topRevenue = root.Q<Label>("top-revenue");
            topPhase = root.Q<Label>("top-phase");
            startTitle = root.Q<Label>("start-title");
            startHint = root.Q<Label>("start-hint");
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                tabs[i] = root.Q<Button>("tab-" + i);
                tabs[i].clicked += () => SelectTab(idx);
                panels[i] = root.Q("panel-" + i);
                panels[i].AddToClassList(i == 1 ? "panel-fleet" : "panel-orders"); // hướng cột riêng từng panel (G3)
                panelHints[i] = root.Q<Label>("panel-" + i + "-hint");
            }
            bottomButton = root.Q<Button>("bottom-button"); // bỏ MAKE IT SO chung (G2.3) — ẩn luôn
            bottomButton.style.display = DisplayStyle.None;
            restartButton = root.Q<Button>("restart-button");
            restartButton.clicked += StartNewRun;
            var exitMachine = root.Q<Button>("exit-machine");
            if (exitMachine != null && machine != null) exitMachine.clicked += machine.ExitTerminal; // FR-0205
            bound = true;
            SelectTab(0);
        }

        void SelectTab(int index)
        {
            for (int i = 0; i < 3; i++)
            {
                tabs[i].EnableInClassList("selected", i == index);
                panels[i].EnableInClassList("on", i == index);
            }
        }

        void LoadWorld()
        {
            if (config != null) return;
            config = DemoData.CreateConfig();
            pool = DemoData.BuildOrderPool();
            incidents = DemoData.BuildIncidentPool();
            crews = DemoData.BuildCrews();
            shipDefs = DemoData.BuildShips();
            endings = DemoData.BuildEndings();
            portNames = new Dictionary<string, string>();
            portDefs = new Dictionary<string, PortDef>();
            var ports = DemoData.BuildPorts();
            foreach (var p in ports)
            {
                portNames[p.portId] = p.displayName;
                portDefs[p.portId] = p;
            }
            routeTable = DemoData.BuildRouteTable();
            mapRouteDefs = DemoData.BuildRouteDefs();
            world = new SimWorld
            {
                Pool = pool, Routes = routeTable, Incidents = incidents.incidents,
                FixedFirstDay = incidents.fixedFirstDay, FixedFinale = incidents.fixedFinale,
                HQPortId = HQPortId(), // G5: hubAffinity của roaming
            };
        }

        // G5: cảng trụ (isHeadquarters) — dùng cho hubAffinity của RoamPlanner.
        string HQPortId()
        {
            foreach (var kv in portDefs)
                if (kv.Value.isHeadquarters) return kv.Key;
            return "";
        }

        void StartNewRun()
        {
            LoadWorld();
            int seed = (state != null && config.repeatSeedOnRestart) ? state.Seed : System.Environment.TickCount;
            state = GameStateBuilder.CreateRun(config, shipDefs, seed);
            OrderScheduler.SpawnInitial(pool, state); // đặt mốc đơn đầu (không còn đơn có sẵn — G7)
            clockAcc = 0f;
            startScreen.style.display = DisplayStyle.None;
            lastRefreshKey = int.MinValue;
            lastPendingIncidents = 0; // ván mới: đếm sự cố về 0, không nhảy tab nhầm
            Refresh();
        }

        void Dirty()
        {
            lastRefreshKey = int.MinValue; // hành động của người chơi đổi state → Refresh ở Update kế
        }

        void Refresh()
        {
            if (state == null) return;
            float t = state.ElapsedSeconds;
            topDay.text = $"Thời gian {Mathf.FloorToInt(t / 60f):0}:{Mathf.FloorToInt(t) % 60:00}";
            topLate.text = $"Trễ {state.LateCount}/{config.maxLateDeliveries}";
            topDelivered.text = $"Đã giao {state.DeliveredCount}/{config.winDeliveries}";
            topRevenue.text = $"Doanh thu {state.Revenue}";

            // FR-0202: huy hiệu tab — số chưa xử lý từng tab; 0 → ẩn.
            int inbox = 0, unassigned = 0;
            foreach (var o in state.Orders)
            {
                if (o.Status == OrderStatus.InInbox) inbox++;
                else if (o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId)) unassigned++;
            }
            int pending = state.PendingIncidents.Count;
            int[] counts = { inbox, unassigned, pending };
            bool incidentPending = pending > 0;
            for (int i = 0; i < 3; i++)
            {
                bool locked = PhaseGate.GetAccess(incidentPending, i) != TabAccess.Interactive;
                tabs[i].EnableInClassList("locked", locked);
                tabs[i].text = TabNames[i] + (counts[i] > 0 ? $" ({counts[i]})" : "");
            }
            // G6.3b: sự cố mới nổ khi đang ở tab khác → tự mở tab SỰ CỐ ngay. Đồng hồ dừng là
            // thiết kế, nhưng không auto-switch thì người chơi chỉ thấy "game đứng" mà không biết
            // phải xử lý ở đâu (bug report 14/11: "giao đơn 2 game đứng luôn, UI lỗi").
            if (incidentPending && lastPendingIncidents == 0) SelectTab(2);
            lastPendingIncidents = pending;

            BuildOrdersPanel();
            BuildFleetPanel();
            BuildIncidentsPanel(incidentPending);

            // Hết ván → màn kết (FR-0701): overlay che toàn kính với nút Restart.
            if (state.IsFinished && state.FinalEnding != EndingId.None)
            {
                string title = "KẾT THÚC VÁN";
                foreach (var e in endings.endings)
                    if (e.id == state.FinalEnding) { title = e.title; break; }
                startTitle.text = title;
                startHint.style.display = DisplayStyle.None;
                restartButton.style.display = DisplayStyle.Flex;
                startScreen.style.display = DisplayStyle.Flex;
            }
        }

        // Hạn đơn — text dùng chung cho lúc dựng và lúc cập nhật 10 Hz.
        static string DeadlineText(OrderState o, GameState s)
            => o.IsLate ? "TRỄ" : $"còn {Mathf.Max(0f, o.DeadlineAtSeconds - s.ElapsedSeconds):0}s";

        // ~10 Hz: sửa text hạn đơn TẠI CHỖ (G2) — không dựng lại panel, không mất cuộn.
        // G4: còn < config.deadlineWarnSeconds → tiền tố "! " + nhấp nháy 0.5s (class blink-off);
        // TRỄ → class late (đảo màu cố định). Số giây lấy từ config, không hard-code.
        void UpdateCountdowns()
        {
            countdownAcc += Time.unscaledDeltaTime;
            if (countdownAcc < 0.1f) return;
            countdownAcc = 0f;
            bool blinkOff = (Mathf.FloorToInt(Time.unscaledTime / 0.5f) & 1) == 1;
            // G7: đếm ngược đơn đầu tiên 10 Hz tại chỗ (label được dựng lại mỗi Refresh).
            if (firstOrderLabel != null && firstOrderLabel.style.display.value == DisplayStyle.Flex)
                firstOrderLabel.text = $"Đơn đầu tiên xuất hiện sau {Mathf.Max(0f, state.NextOrderSpawnAtSeconds - state.ElapsedSeconds):0}s — đọc hướng dẫn dưới.";
            foreach (var (o, label, prefix, suffix) in countLabels)
            {
                if (label == null) continue;
                bool warn = !o.IsLate && o.DeadlineAtSeconds - state.ElapsedSeconds < config.deadlineWarnSeconds;
                label.text = (warn ? "! " : "") + prefix + DeadlineText(o, state) + suffix;
                label.EnableInClassList("blink-off", warn && blinkOff);
                label.EnableInClassList("late", o.IsLate);
            }
            // G6.3b: sự cố đang chờ → nhấp nháy tab SỰ CỐ (class blink-off tái dùng của cảnh báo hạn).
            if (tabs[2] != null) tabs[2].EnableInClassList("blink-off",
                state.PendingIncidents.Count > 0 && blinkOff);
            // G7/G5: ETA tại chỗ — tàu Roaming (tốc × roamSpeedFactor) lẫn tàu Moving
            // (còn = (length − đã đi) / tốc) đều sửa text 10 Hz, không dựng lại panel.
            foreach (var (ship, label, prefix, suffix) in shipEtaLabels)
            {
                if (label == null || ship == null) continue;
                if (ship.Route == null) { label.text = prefix + "?" + suffix; continue; }
                float remain = ship.Status == ShipStatus.Roaming
                    ? CommandValidator.RoamRemainSeconds(ship, state.Config)
                    : Mathf.Max(0f, (ship.Route.length - ship.DistanceAlongRoute) / state.Config.SpeedUnitsPerSecond(ship.Speed));
                label.text = prefix + Mathf.CeilToInt(remain) + suffix;
            }
        }

        Label CountdownLabel(OrderState o, string prefix, string suffix = "")
        {
            var label = new Label(prefix + DeadlineText(o, state) + suffix);
            label.AddToClassList("order-line");
            countLabels.Add((o, label, prefix, suffix));
            return label;
        }

        // ============================================== Bước 3: tab ĐƠN HÀNG (FR-03xx)
        // Render-only: đọc state + config, phát intent qua OrderActions — không chứa luật game.

        void BuildOrdersPanel()
        {
            var panel = panels[0];
            var oldScroll = panel.Q<ScrollView>();
            float savedOffset = oldScroll != null ? oldScroll.scrollOffset.y : 0f; // giữ vị trí cuộn qua lần dựng lại
            panel.Clear();
            countLabels.Clear(); // đăng ký hạn đơn dựng lại từ đầu mỗi lần Refresh

            var scroll = CRTScroll();
            scroll.AddToClassList("orders-scroll");
            panel.Add(scroll);

            // Dòng tóm tắt đầu tab (FR-0303): số đơn đang hoạt động, số liệu từ config.
            int activeCount = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned).Count;
            var summary = new Label($"Đơn đang hoạt động {activeCount}/{config.maxActiveOrders}");
            summary.AddToClassList("order-summary");
            scroll.Add(summary);

            // G7: ván KHÔNG còn đơn có sẵn — đếm ngược tới đơn đầu tiên (10 Hz, sửa text tại chỗ).
            if (state.OrderSpawnCount == 0 && state.ElapsedSeconds < state.NextOrderSpawnAtSeconds)
            {
                if (firstOrderLabel == null)
                {
                    firstOrderLabel = new Label("");
                    firstOrderLabel.AddToClassList("order-line");
                }
                firstOrderLabel.style.display = DisplayStyle.Flex;
                firstOrderLabel.text = $"Đơn đầu tiên xuất hiện sau {Mathf.Max(0f, state.NextOrderSpawnAtSeconds - state.ElapsedSeconds):0}s — đọc hướng dẫn dưới.";
                scroll.Add(firstOrderLabel);
            }
            else if (firstOrderLabel != null) firstOrderLabel.style.display = DisplayStyle.None;

            // Hướng dẫn cách chơi (bỏ màn BẮT ĐẦU — review 14/11): hiện đến khi người chơi nhận đơn đầu tiên.
            // Cờ lấy từ state (Delivered/Late/đang chạy), không phải trạng thái UI — Refresh dựng lại vẫn đúng;
            // Delivered/Late giữ cờ sau khi giao xong nên clue không nháy lại giữa ván; ván mới (CHƠI LẠI) tự reset.
            if (activeCount == 0 && state.DeliveredCount == 0 && state.LateCount == 0)
                scroll.Add(Clue($"Bấm NHẬN ở tab này → điều tàu ở tab ĐỘI TÀU: bấm tuyến là tàu khởi hành NGAY (MAKE IT SO). Đồng hồ chạy liên tục: giao đủ {config.winDeliveries} đơn là THẮNG, trễ {config.maxLateDeliveries} đơn là THUA. Sự cố chờ xử lý ở tab SỰ CỐ — đồng hồ dừng đến khi chọn xong phương án."));

            // ĐƠN ĐÃ NHẬN lên trước hộp thư: người chơi thấy kết quả ngay khi bấm NHẬN.
            scroll.Add(Section("ĐƠN ĐÃ NHẬN"));
            var active = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned);
            if (active.Count == 0) scroll.Add(Line("Chưa nhận đơn nào."));
            foreach (var o in active) scroll.Add(ActiveRow(o));

            scroll.Add(Section("HỘP THƯ"));
            var note = new Label($"Đơn trễ chỉ nhận doanh thu ×{config.lateRevenueFactor:0.##} (áp cho mọi đơn)");
            note.AddToClassList("order-line");
            note.AddToClassList("order-clue");
            scroll.Add(note);
            var inbox = state.Orders.FindAll(o => o.Status == OrderStatus.InInbox);
            if (inbox.Count == 0) scroll.Add(Line("Hộp thư trống."));
            foreach (var o in inbox) scroll.Add(InboxCard(o));

            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, savedOffset)).ExecuteLater(1);
        }

        // ScrollView đồng bộ kiểu CRT: ẩn thanh ngang + bước cuộn lăn chuột (hằng UX, không phải số cân bằng).
        static ScrollView CRTScroll()
        {
            var s = new ScrollView();
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            s.mouseWheelScrollSize = 60f;
            return s;
        }

        // ============================================== Bước 5: tab SỰ CỐ (FR-05xx)
        // Render-only: đọc PendingIncidents, phát intent qua IncidentResolver — đồng hồ dừng đến khi chọn xong.

        string lastIncidentLine; // kết quả lựa chọn gần nhất (hiện cho đến sự cố kế tiếp)

        void BuildIncidentsPanel(bool incidentPending)
        {
            var panel = panels[2];
            panel.Clear();
            var scroll = CRTScroll();
            scroll.AddToClassList("orders-scroll");
            panel.Add(scroll);

            if (!incidentPending)
            {
                scroll.Add(Section("SỰ CỐ"));
                scroll.Add(Line("Chưa có sự cố. Mừng quá sớm?"));
                if (!string.IsNullOrEmpty(lastIncidentLine)) scroll.Add(Clue(lastIncidentLine));
                return;
            }

            scroll.Add(Section("SỰ CỐ ĐANG CHỜ — đồng hồ dừng"));
            foreach (var p in state.PendingIncidents)
            {
                var card = new VisualElement();
                card.AddToClassList("order-card");
                card.Add(Title(p.Incident.title));
                card.Add(Line(p.Incident.situationText));
                if (!string.IsNullOrEmpty(p.Incident.clueText)) card.Add(Clue(p.Incident.clueText));
                var ship = p.Context.Ship;
                if (ship != null)
                    card.Add(Line($"Tàu {ship.DisplayName} — thuyền trưởng {CaptainName(ship.Captain)}"));
                var row = new VisualElement();
                row.AddToClassList("btn-row");
                foreach (var opt in p.Incident.options)
                {
                    var captured = p;
                    var capturedOpt = opt;
                    var btn = new Button(() => ResolvePendingIncident(captured, capturedOpt)) { text = opt.label };
                    btn.AddToClassList("btn-accept");
                    row.Add(btn);
                }
                card.Add(row);
                scroll.Add(card);
            }
            if (!string.IsNullOrEmpty(lastIncidentLine)) scroll.Add(Clue(lastIncidentLine));
        }

        void ResolvePendingIncident(PendingIncident p, IncidentOption opt)
        {
            var resolved = IncidentResolver.Resolve(p.Incident, opt, state, p.Context.Ship, crews);
            IncidentResolver.Apply(resolved, state, p.Context.Ship);
            Simulation.ApplyProgressDelta(state, p.Context.Ship, resolved.ProgressDelta);
            state.PendingIncidents.Remove(p);
            lastIncidentLine = $"{p.Incident.title} — {resolved.OptionLabel}: {resolved.Explanation} “{resolved.Quote}”";
            Dirty(); // đồng hồ chạy lại, mọi panel vẽ lại theo state mới
        }

        VisualElement InboxCard(OrderState o)
        {
            var card = new VisualElement();
            card.AddToClassList("order-card");
            card.Add(Title(o.CustomerName));
            var main = new VisualElement();
            main.AddToClassList("order-main");
            main.Add(Line($"{PortName(o.FromPortId)} → {PortName(o.ToPortId)}"));
            var chips = new VisualElement();
            chips.AddToClassList("chip-row");
            foreach (var (tag, name) in CargoTags(o.Tags)) chips.Add(Chip(CargoChipClass(tag), name));
            main.Add(chips);
            card.Add(main);
            card.Add(CountdownLabel(o, "Hạn: ", $" · Doanh thu {o.Revenue}"));
            if (!string.IsNullOrEmpty(o.Clue)) card.Add(Clue(o.Clue));

            var row = new VisualElement();
            row.AddToClassList("btn-row");
            // FR-0303: vượt giới hạn đơn đang hoạt động → NHẬN vô hiệu kèm lý do.
            bool canAccept = CommandValidator.CanAccept(state, o.Id);
            var accept = new Button(() => { OrderActions.Accept(state, o.Id); Dirty(); }) { text = "NHẬN" };
            accept.AddToClassList("btn-accept");
            accept.SetEnabled(canAccept);
            row.Add(accept);
            if (!canAccept)
            {
                var reason = Warn($"Đã đủ {config.maxActiveOrders} đơn đang hoạt động");
                row.Add(reason);
            }
            var decline = new Button(() => { OrderActions.Decline(state, o.Id); Dirty(); }) { text = "TỪ CHỐI" };
            decline.AddToClassList("btn-decline");
            row.Add(decline);
            card.Add(row);
            return card;
        }

        VisualElement ActiveRow(OrderState o)
        {
            var row = new VisualElement();
            row.AddToClassList("order-active");
            string ship = "";
            if (!string.IsNullOrEmpty(o.AssignedShipId))
            {
                var s = state.Ships.Find(x => x.Id == o.AssignedShipId);
                if (s != null) ship = $" · tàu {s.DisplayName}";
            }
            string status = o.Status == OrderStatus.Assigned ? "Đang chở" : "Chưa có tàu";
            row.Add(CountdownLabel(o, $"{o.CustomerName} — {status}{ship} · {PortName(o.FromPortId)} → {PortName(o.ToPortId)} · Hạn: "));
            return row;
        }

        static IEnumerable<(OrderTag tag, string name)> CargoTags(OrderTag tags)
        {
            // G4: ký hiệu thay màu — phân biệt loại hàng khi CRT đơn sắc.
            if ((tags & OrderTag.Fragile) != 0) yield return (OrderTag.Fragile, "[!] Dễ vỡ");
            if ((tags & OrderTag.Cold) != 0) yield return (OrderTag.Cold, "[*] Lạnh");
            if ((tags & OrderTag.Urgent) != 0) yield return (OrderTag.Urgent, "[>>] Khẩn");
            if (tags == OrderTag.None) yield return (OrderTag.None, "Hàng Thường");
        }

        static string CargoChipClass(OrderTag tag) => tag switch
        {
            OrderTag.Fragile => "chip-fragile",
            OrderTag.Cold => "chip-cold",
            OrderTag.Urgent => "chip-urgent",
            _ => "chip-plain",
        };

        static VisualElement Chip(string cls, string text)
        {
            var label = new Label(text);
            label.AddToClassList("chip");
            label.AddToClassList(cls);
            return label;
        }

        string PortName(string portId)
        {
            return portNames != null && portNames.TryGetValue(portId, out var name) ? name : portId;
        }

        static VisualElement Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("section-title");
            return label;
        }

        static VisualElement Title(string text)
        {
            var label = new Label(text);
            label.AddToClassList("order-title");
            return label;
        }

        static VisualElement Line(string text)
        {
            var label = new Label(text);
            label.AddToClassList("order-line");
            return label;
        }

        static VisualElement Clue(string text)
        {
            var label = new Label("» " + text);
            label.AddToClassList("order-line");
            label.AddToClassList("order-clue");
            label.style.whiteSpace = WhiteSpace.Normal; // clue dài cần xuống dòng (mặc định Label không wrap)
            return label;
        }

        // G4: cảnh báo đơn sắc — cùng phosphor sáng, phân biệt bằng tiền tố "! ".
        static VisualElement Warn(string text)
        {
            var label = new Label("! " + text);
            label.AddToClassList("order-line");
            label.AddToClassList("order-warn");
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
