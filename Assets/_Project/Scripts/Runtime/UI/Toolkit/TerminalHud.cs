using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AyeAye
{
    // FR-0201..0203: khung UI Toolkit — thanh trên, 3 tab + khóa theo pha (PhaseGate), nút dưới, Start/Restart.
    // Bước 2 chỉ dựng khung: nội dung tab là placeholder; MAKE IT SO chạy ngày bằng BalanceProbe (tự chơi thay người).
    // shortcut: ngày chạy tự động, tab trả về dữ liệu thật ở Bước 3-5; lúc đó bỏ loop này.
    [RequireComponent(typeof(UIDocument))]
    public class TerminalHud : MonoBehaviour
    {
        static readonly string[] PhaseNames = { "Lập kế hoạch", "Diễn tập", "Sự cố", "Tổng kết" };
        static readonly string[] TabNames = { "ĐƠN HÀNG", "ĐỘI TÀU", "SỰ CỐ" };

        UIDocument document;
        TerminalView machine;

        VisualElement game, startScreen, bottomBar;
        Label topDay, topReputation, topRevenue, topPhase, startTitle;
        Button[] tabs = new Button[3];
        VisualElement[] panels = new VisualElement[3];
        Button bottomButton, startButton, restartButton;
        Label[] panelHints = new Label[3];

        bool bound;
        GameState state;
        int lastRefreshKey = int.MinValue;

        // Dữ liệu mẫu §5 (nguồn duy nhất DemoData, khớp TestWorld + Build Demo).
        GameConfig config;
        OrderPool pool;
        IncidentPool incidents;
        List<CrewDefinition> crews;
        List<ShipDef> shipDefs;
        EndingDefinitions endings;
        Dictionary<string, string> portNames;

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
                int key = state.CurrentDay * 100 + (int)state.Phase * 10 + (state.IsFinished ? 1 : 0);
                if (key != lastRefreshKey) { lastRefreshKey = key; Refresh(); }
            }
        }

        void TryBind()
        {
            if (document == null || bound) return;
            var root = document.rootVisualElement;
            if (root == null || root.childCount == 0) return;

            game = root.Q("game");
            startScreen = root.Q("start-screen");
            bottomBar = root.Q("bottombar");
            topDay = root.Q<Label>("top-day");
            topReputation = root.Q<Label>("top-reputation");
            topRevenue = root.Q<Label>("top-revenue");
            topPhase = root.Q<Label>("top-phase");
            startTitle = root.Q<Label>("start-title");
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                tabs[i] = root.Q<Button>("tab-" + i);
                tabs[i].clicked += () => SelectTab(idx);
                panels[i] = root.Q("panel-" + i);
                panelHints[i] = root.Q<Label>("panel-" + i + "-hint");
            }
            bottomButton = root.Q<Button>("bottom-button");
            bottomButton.clicked += OnBottomButton;
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
            if (state != null && PhaseGate.TabAccess(state.Phase, index) == TabAccess.Locked) return;
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
            foreach (var p in DemoData.BuildPorts())
                portNames[p.portId] = p.displayName;
        }

        void StartNewRun()
        {
            LoadWorld();
            int seed = (state != null && config.repeatSeedOnRestart) ? state.Seed : System.Environment.TickCount;
            state = GameStateBuilder.CreateRun(config, shipDefs, seed);
            OrderScheduler.ScheduleNewOrders(pool, state); // Ngày 1: 2 đơn cố định (FR-0301)
            startScreen.style.display = DisplayStyle.None;
            lastRefreshKey = int.MinValue;
            Refresh();
        }

        void Dirty()
        {
            lastRefreshKey = int.MinValue; // hành động của người chơi đổi state → Refresh ở Update kế
        }

        void OnBottomButton()
        {
            if (state == null || state.IsFinished) return;
            switch (PhaseGate.BottomButton(state.Phase))
            {
                case BottomAction.MakeItSo:
                    DayRunner.CommitPlanning(state);
                    DayRunner.Simulate(state, incidents.incidents, incidents.fixedFirstDay,
                        incidents.fixedFinale, crews);
                    Dirty();
                    break;
                // Continue: dây ở Bước 5 (tab SỰ CỐ tương tác).
                case BottomAction.NextDay:
                    DayRunner.CompleteSummary(state, pool);
                    Dirty();
                    break;
            }
        }

        void Refresh()
        {
            if (state == null) return;
            topDay.text = $"Ngày {state.CurrentDay}/{config.gameLengthDays}";
            topReputation.text = $"Uy tín {state.Reputation}";
            topRevenue.text = $"Doanh thu {state.Revenue}";
            topPhase.text = PhaseNames[(int)state.Phase];

            // FR-0202: huy hiệu tab — số chưa xử lý từng tab; 0 → ẩn.
            int inbox = 0, unassigned = 0;
            foreach (var o in state.Orders)
            {
                if (o.Status == OrderStatus.InInbox) inbox++;
                else if (o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId)) unassigned++;
            }
            // Bước 2: sự cố tự xử trong loop → badge 3 = 0 (Bước 5 đếm sự cố chưa xử).
            int[] counts = { inbox, unassigned, 0 };
            for (int i = 0; i < 3; i++)
            {
                bool locked = PhaseGate.TabAccess(state.Phase, i) == TabAccess.Locked;
                tabs[i].EnableInClassList("locked", locked);
                tabs[i].text = TabNames[i] + (counts[i] > 0 ? $" ({counts[i]})" : "");
            }

            // panel 3 (SỰ CỐ) trống ở pha Planning → thông điệp FR-0203.
            panelHints[2].text = state.Phase == GamePhase.Planning
                ? "Chưa có sự cố. Mừng quá sớm?"
                : "Sự cố đang xử lý...";

            BuildOrdersPanel();

            // Nút dưới theo pha (FR-0203).
            var action = PhaseGate.BottomButton(state.Phase);
            bool hidden = action == BottomAction.Hidden;
            bottomBar.EnableInClassList("hidden", hidden);
            if (!hidden)
                bottomButton.text = action == BottomAction.MakeItSo ? "MAKE IT SO"
                    : action == BottomAction.Continue ? "TIẾP TỤC" : "NGÀY KẺ";

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
            panel.Clear();

            var scroll = new ScrollView();
            scroll.AddToClassList("orders-scroll");
            panel.Add(scroll);

            scroll.Add(Section("HỘP THƯ"));
            var inbox = state.Orders.FindAll(o => o.Status == OrderStatus.InInbox);
            if (inbox.Count == 0) scroll.Add(Line("Hộp thư trống."));
            foreach (var o in inbox) scroll.Add(InboxCard(o));

            scroll.Add(Section("ĐƠN ĐÃ NHẬN"));
            var active = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned);
            if (active.Count == 0) scroll.Add(Line("Chưa nhận đơn nào."));
            foreach (var o in active) scroll.Add(ActiveRow(o));
        }

        VisualElement InboxCard(OrderState o)
        {
            var card = new VisualElement();
            card.AddToClassList("order-card");
            card.Add(Title(o.CustomerName));
            card.Add(Line($"{CargoLabel(o.Tags)} · {PortName(o.FromPortId)} → {PortName(o.ToPortId)}"));
            card.Add(Line($"Hạn: Ngày {o.DeadlineDay} · Doanh thu {o.Revenue} · " +
                $"Trễ: Uy tín {config.lateReputationPenalty}, doanh thu ×{config.lateRevenueFactor:0.##}"));
            if (!string.IsNullOrEmpty(o.Clue)) card.Add(Clue(o.Clue));

            var row = new VisualElement();
            row.AddToClassList("btn-row");
            // FR-0303: vượt giới hạn đơn đang hoạt động → NHẬN vô hiệu kèm lý do.
            bool canAccept = CommandValidator.CanAccept(state, o.Id);
            var accept = new Button(() => { OrderActions.Accept(state, o.Id); Dirty(); }) { text = "NHẬN" };
            accept.SetEnabled(canAccept);
            row.Add(accept);
            if (!canAccept)
            {
                var reason = new Label($"Đã đủ {config.maxActiveOrders} đơn đang hoạt động");
                reason.AddToClassList("order-line");
                reason.AddToClassList("order-warn");
                row.Add(reason);
            }
            row.Add(new Button(() => { OrderActions.Decline(state, o.Id); Dirty(); }) { text = "TỪ CHỐI" });
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
            row.Add(Title($"{o.CustomerName} — {status}{ship}"));
            row.Add(Line($"{CargoLabel(o.Tags)} · {PortName(o.FromPortId)} → {PortName(o.ToPortId)} · Hạn: Ngày {o.DeadlineDay}"));
            return row;
        }

        static string CargoLabel(OrderTag tags)
        {
            var names = new List<string>();
            if ((tags & OrderTag.Fragile) != 0) names.Add("Dễ vỡ");
            if ((tags & OrderTag.Cold) != 0) names.Add("Lạnh");
            if ((tags & OrderTag.Urgent) != 0) names.Add("Khẩn");
            return names.Count == 0 ? "Hàng Thường" : "Hàng " + string.Join(" + ", names);
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
