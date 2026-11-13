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
        Label topDay, topLate, topRevenue, topPhase, startTitle;
        Button[] tabs = new Button[3];
        VisualElement[] panels = new VisualElement[3];
        Button bottomButton, startButton, restartButton;
        Label[] panelHints = new Label[3];

        bool bound;
        GameState state;
        SimWorld world;                 // dữ liệu tĩnh của ván cho Simulation.Advance
        float clockAcc;                 // dồn dt thật → chạy Advance theo bước cố định (chỉnh 3)
        int lastRefreshKey = int.MinValue;

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

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            machine = GetComponent<TerminalView>();
        }

        void Update()
        {
            if (!bound) { TryBind(); if (!bound) return; }
            if (machine != null && game != null)
                game.EnableInClassList("on", machine.IsMachineOn);
            // Máy bật lần đầu: vào thẳng ván mới — bỏ màn "AYE AYE, BOSS..." (boss duyệt bỏ 12/11/2026).
            if (machine != null && machine.IsMachineOn && state == null)
                StartNewRun();
            if (state != null)
            {
                RunClock();
                int inboxCount = 0;
                foreach (var o in state.Orders) if (o.Status == OrderStatus.InInbox) inboxCount++;
                int key = state.LateCount * 1000 + state.DeliveredCount * 100
                    + (state.PendingIncidents.Count > 0 ? 10 : 0) + (state.IsFinished ? 1 : 0)
                    + inboxCount; // đơn mới vào hộp thư / hết hạn cũng cần vẽ lại
                if (key != lastRefreshKey) { lastRefreshKey = key; Refresh(); }
                UpdateFleetDots(); // chấm tàu theo state mỗi frame (G2.5 shim; panel dựng lại chỉ khi key đổi)
            }
        }

        // G1 shim: dồn dt thật rồi chạy Simulation.Advance theo bước simTickSeconds (giới hạn tick mỗi frame).
        // Tạm dừng tự nhiên khi máy tắt (không gọi), sự cố chờ hoặc kết thúc (CanAdvance == false).
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
            topRevenue = root.Q<Label>("top-revenue");
            topPhase = root.Q<Label>("top-phase");
            startTitle = root.Q<Label>("start-title");
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
            startButton = root.Q<Button>("start-button");
            startButton.clicked += StartNewRun;
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
            };
        }

        void StartNewRun()
        {
            LoadWorld();
            int seed = (state != null && config.repeatSeedOnRestart) ? state.Seed : System.Environment.TickCount;
            state = GameStateBuilder.CreateRun(config, shipDefs, seed);
            OrderScheduler.SpawnInitial(pool, state); // 2 đơn đầu + bắt đầu nhịp spawn (FR-0301)
            clockAcc = 0f;
            startScreen.style.display = DisplayStyle.None;
            lastRefreshKey = int.MinValue;
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
            topRevenue.text = $"Doanh thu {state.Revenue}";
            topPhase.text = state.CanAdvance ? "ĐANG CHẠY" : "TẠM DỪNG";

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

            panelHints[2].text = incidentPending
                ? "Sự cố đang chờ — xử lý ở tab SỰ CỐ."
                : "Chưa có sự cố. Mừng quá sớm?";

            BuildOrdersPanel();
            BuildFleetPanel();

            // Hết ván → màn kết (FR-0701): overlay che toàn kính với nút Restart.
            if (state.IsFinished && state.FinalEnding != EndingId.None)
            {
                string title = "KẾT THÚC VÁN";
                foreach (var e in endings.endings)
                    if (e.id == state.FinalEnding) { title = e.title; break; }
                startTitle.text = title;
                startButton.style.display = DisplayStyle.None;
                startScreen.style.display = DisplayStyle.Flex;
            }
        }

        // ============================================== Bước 3: tab ĐƠN HÀNG (FR-03xx)
        // Render-only: đọc state + config, phát intent qua OrderActions — không chứa luật game.

        void BuildOrdersPanel()
        {
            var panel = panels[0];
            var oldScroll = panel.Q<ScrollView>();
            float savedOffset = oldScroll != null ? oldScroll.scrollOffset.y : 0f; // giữ vị trí cuộn qua lần dựng lại
            panel.Clear();

            var scroll = CRTScroll();
            scroll.AddToClassList("orders-scroll");
            panel.Add(scroll);

            // Dòng tóm tắt đầu tab (FR-0303): số đơn đang hoạt động, số liệu từ config.
            int activeCount = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned).Count;
            var summary = new Label($"Đơn đang hoạt động {activeCount}/{config.maxActiveOrders}");
            summary.AddToClassList("order-summary");
            scroll.Add(summary);

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
            float left = Mathf.Max(0f, o.DeadlineAtSeconds - state.ElapsedSeconds);
            string limit = o.IsLate ? "TRỄ" : $"còn {left:0}s";
            card.Add(Line($"Hạn: {limit} · Doanh thu {o.Revenue}"));
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
                var reason = new Label($"Đã đủ {config.maxActiveOrders} đơn đang hoạt động");
                reason.AddToClassList("order-line");
                reason.AddToClassList("order-warn");
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
            string limit = o.IsLate ? "TRỄ" : $"còn {Mathf.Max(0f, o.DeadlineAtSeconds - state.ElapsedSeconds):0}s";
            row.Add(Line($"{o.CustomerName} — {status}{ship} · {PortName(o.FromPortId)} → {PortName(o.ToPortId)} · Hạn: {limit}"));
            return row;
        }

        static IEnumerable<(OrderTag tag, string name)> CargoTags(OrderTag tags)
        {
            if ((tags & OrderTag.Fragile) != 0) yield return (OrderTag.Fragile, "Dễ vỡ");
            if ((tags & OrderTag.Cold) != 0) yield return (OrderTag.Cold, "Lạnh");
            if ((tags & OrderTag.Urgent) != 0) yield return (OrderTag.Urgent, "Khẩn");
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
            return label;
        }
    }
}
