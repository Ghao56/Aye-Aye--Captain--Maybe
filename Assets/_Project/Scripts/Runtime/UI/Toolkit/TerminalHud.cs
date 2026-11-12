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
        RouteTable routes;
        IncidentPool incidents;
        List<CrewDefinition> crews;
        List<ShipDef> shipDefs;
        EndingDefinitions endings;

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
            routes = DemoData.BuildRouteTable();
            incidents = DemoData.BuildIncidentPool();
            crews = DemoData.BuildCrews();
            shipDefs = DemoData.BuildShips();
            endings = DemoData.BuildEndings();
        }

        void StartNewRun()
        {
            LoadWorld();
            int seed = (state != null && config.repeatSeedOnRestart) ? state.Seed : System.Environment.TickCount;
            state = GameStateBuilder.CreateRun(config, shipDefs, seed);
            startScreen.style.display = DisplayStyle.None;
            lastRefreshKey = int.MinValue;
            Refresh();
        }

        void OnBottomButton()
        {
            if (state == null || state.IsFinished) return;
            switch (PhaseGate.BottomButton(state.Phase))
            {
                case BottomAction.MakeItSo:
                    // Bước 2: tự chơi thay người — Bước 3-5 thay bằng tab thật.
                    BalanceProbe.SimulateDay(config, pool, routes,
                        incidents.incidents, incidents.fixedFirstDay, incidents.fixedFinale, crews, state);
                    lastRefreshKey = int.MinValue;
                    Refresh();
                    break;
                // Continue / NextDay: dây ở Bước 5 (SỰ CỐ + tổng kết).
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
    }
}
