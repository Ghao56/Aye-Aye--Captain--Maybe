using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace AyeAye
{
    [RequireComponent(typeof(UIDocument))]

    public partial class TerminalHud : MonoBehaviour
    {
        static readonly string[] TabNames = { "ORDERS", "FLEET", "INCIDENTS" };
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
        SimWorld world;
        float clockAcc;
        int lastRefreshKey = int.MinValue;
        bool focusPaused;
        [Tooltip("DEBUG in editor: skip the focus-lost pause (to inspect the UI while the window is unfocused).")]

        public bool ignoreFocusPause = false;
        Label firstOrderLabel;
        float countdownAcc;
        readonly List<(OrderState order, Label label, string prefix, string suffix)> countLabels
            = new List<(OrderState, Label, string, string)>();
        readonly List<(ShipState ship, Label label, string prefix, string suffix)> shipEtaLabels
            = new List<(ShipState, Label, string, string)>();
        GameConfig config;
        OrderPool pool;
        IncidentPool incidents;
        List<CrewDefinition> crews;
        List<ShipDef> shipDefs;
        EndingDefinitions endings;
        Dictionary<string, string> portNames;
        Dictionary<string, PortDef> portDefs;
        RouteTable routeTable;
        List<RouteDef> mapRouteDefs;
        int lastPendingIncidents;
        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            machine = GetComponent<TerminalView>();
            Application.focusChanged += OnFocusChanged;
        }
        void OnDisable()
        {
            Application.focusChanged -= OnFocusChanged;
        }
        void OnFocusChanged(bool focus) => focusPaused = !focus;
        void OnApplicationFocus(bool hasFocus) => focusPaused = !hasFocus;
        void OnApplicationPause(bool paused) { if (paused) focusPaused = true; }
        void Update()
        {
            if (!bound) { TryBind(); if (!bound) return; }
            if (machine != null && game != null)
                game.EnableInClassList("on", machine.IsMachineOn);
            if (machine != null && machine.IsMachineOn && state == null)
                StartNewRun();
            if (state != null && machine != null && machine.IsMachineOn && (ignoreFocusPause || !focusPaused))
            {
                RunClock();
                int inboxCount = 0;
                foreach (var o in state.Orders) if (o.Status == OrderStatus.InInbox) inboxCount++;
                int key = state.LateCount * 1000 + state.DeliveredCount * 100
                    + (state.PendingIncidents.Count > 0 ? 10 : 0) + (state.IsFinished ? 1 : 0)
                    + inboxCount;
                foreach (var s in state.Ships)
                {
                    key = key * 31 + (int)s.Status;
                    key = key * 31 + (s.PortId != null ? s.PortId.GetHashCode() : 0);
                    key = key * 31 + (s.Route != null && s.Route.ToPort != null ? s.Route.ToPort.GetHashCode() : 0);
                }
                if (key != lastRefreshKey) { Refresh(); lastRefreshKey = key; }
                UpdateCountdowns();
            }
            if (state != null)
            {
                UpdateTopPhase();
                UpdateFleetDots();
            }
        }
        void UpdateTopPhase()
        {
            if (topPhase == null) return;
            if (focusPaused && !ignoreFocusPause) topPhase.text = "PAUSED (no focus)";
            else if (state.IsFinished) topPhase.text = "FINISHED";
            else if (!state.CanAdvance) topPhase.text = "PAUSED (incident)";
            else topPhase.text = "RUNNING";
        }
        void RunClock()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.5f);
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
                panels[i].AddToClassList(i == 1 ? "panel-fleet" : "panel-orders");
                panelHints[i] = root.Q<Label>("panel-" + i + "-hint");
            }
            bottomButton = root.Q<Button>("bottom-button");
            bottomButton.style.display = DisplayStyle.None;
            restartButton = root.Q<Button>("restart-button");
            restartButton.clicked += StartNewRun;
            var exitMachine = root.Q<Button>("exit-machine");
            if (exitMachine != null && machine != null) exitMachine.clicked += machine.ExitTerminal;
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
                HQPortId = HQPortId(),
            };
        }
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
            OrderScheduler.SpawnInitial(pool, state);
            clockAcc = 0f;
            startScreen.style.display = DisplayStyle.None;
            lastRefreshKey = int.MinValue;
            lastPendingIncidents = 0;
            Refresh();
        }
        void Dirty()
        {
            lastRefreshKey = int.MinValue;
        }
        void Refresh()
        {
            if (state == null) return;
            float t = state.ElapsedSeconds;
            topDay.text = $"Time {Mathf.FloorToInt(t / 60f):0}:{Mathf.FloorToInt(t) % 60:00}";
            topLate.text = $"Late {state.LateCount}/{config.maxLateDeliveries}";
            topDelivered.text = $"Delivered {state.DeliveredCount}/{config.winDeliveries}";
            topRevenue.text = $"Revenue {state.Revenue}";
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
            if (incidentPending && lastPendingIncidents == 0) SelectTab(2);
            lastPendingIncidents = pending;
            BuildOrdersPanel();
            BuildFleetPanel();
            BuildIncidentsPanel(incidentPending);
            if (state.IsFinished && state.FinalEnding != EndingId.None)
            {
                string title = "RUN COMPLETE";
                foreach (var e in endings.endings)
                    if (e.id == state.FinalEnding) { title = e.title; break; }
                startTitle.text = title;
                startHint.style.display = DisplayStyle.None;
                restartButton.style.display = DisplayStyle.Flex;
                startScreen.style.display = DisplayStyle.Flex;
            }
        }
        static string DeadlineText(OrderState o, GameState s)
            => o.IsLate ? "LATE" : $"{Mathf.Max(0f, o.DeadlineAtSeconds - s.ElapsedSeconds):0}s left";
        void UpdateCountdowns()
        {
            countdownAcc += Time.unscaledDeltaTime;
            if (countdownAcc < 0.1f) return;
            countdownAcc = 0f;
            bool blinkOff = (Mathf.FloorToInt(Time.unscaledTime / 0.5f) & 1) == 1;
            if (firstOrderLabel != null && firstOrderLabel.style.display.value == DisplayStyle.Flex)
                firstOrderLabel.text = $"First order in {Mathf.Max(0f, state.NextOrderSpawnAtSeconds - state.ElapsedSeconds):0}s — read the guide below.";
            foreach (var (o, label, prefix, suffix) in countLabels)
            {
                if (label == null) continue;
                bool warn = !o.IsLate && o.DeadlineAtSeconds - state.ElapsedSeconds < config.deadlineWarnSeconds;
                label.text = (warn ? "! " : "") + prefix + DeadlineText(o, state) + suffix;
                label.EnableInClassList("blink-off", warn && blinkOff);
                label.EnableInClassList("late", o.IsLate);
            }
            if (tabs[2] != null) tabs[2].EnableInClassList("blink-off",
                state.PendingIncidents.Count > 0 && blinkOff);
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
        void BuildOrdersPanel()
        {
            var panel = panels[0];
            var oldScroll = panel.Q<ScrollView>();
            float savedOffset = oldScroll != null ? oldScroll.scrollOffset.y : 0f;
            panel.Clear();
            countLabels.Clear();
            var scroll = CRTScroll();
            scroll.AddToClassList("orders-scroll");
            panel.Add(scroll);
            int activeCount = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned).Count;
            var summary = new Label($"Active orders {activeCount}/{config.maxActiveOrders}");
            summary.AddToClassList("order-summary");
            scroll.Add(summary);
            if (state.OrderSpawnCount == 0 && state.ElapsedSeconds < state.NextOrderSpawnAtSeconds)
            {
                if (firstOrderLabel == null)
                {
                    firstOrderLabel = new Label("");
                    firstOrderLabel.AddToClassList("order-line");
                }
                firstOrderLabel.style.display = DisplayStyle.Flex;
                firstOrderLabel.text = $"First order in {Mathf.Max(0f, state.NextOrderSpawnAtSeconds - state.ElapsedSeconds):0}s — read the guide below.";
                scroll.Add(firstOrderLabel);
            }
            else if (firstOrderLabel != null) firstOrderLabel.style.display = DisplayStyle.None;
            if (activeCount == 0 && state.DeliveredCount == 0 && state.LateCount == 0)
                scroll.Add(Clue($"Press ACCEPT in this tab → command ships in the FLEET tab: click a route and the ship departs NOW (MAKE IT SO). The clock never stops: deliver {config.winDeliveries} orders to WIN, be late on {config.maxLateDeliveries} to LOSE. Pending incidents are in the INCIDENTS tab — the clock pauses until you pick an option."));
            scroll.Add(Section("ACCEPTED ORDERS"));
            var active = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned);
            if (active.Count == 0) scroll.Add(Line("No orders accepted yet."));
            foreach (var o in active) scroll.Add(ActiveRow(o));
            scroll.Add(Section("INBOX"));
            var note = new Label($"Late orders only earn ×{config.lateRevenueFactor:0.##} revenue (applies to all orders)");
            note.AddToClassList("order-line");
            note.AddToClassList("order-clue");
            scroll.Add(note);
            var inbox = state.Orders.FindAll(o => o.Status == OrderStatus.InInbox);
            if (inbox.Count == 0) scroll.Add(Line("Inbox is empty."));
            foreach (var o in inbox) scroll.Add(InboxCard(o));
            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, savedOffset)).ExecuteLater(1);
        }
        static ScrollView CRTScroll()
        {
            var s = new ScrollView();
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            s.mouseWheelScrollSize = 60f;
            return s;
        }
        string lastIncidentLine;
        void BuildIncidentsPanel(bool incidentPending)
        {
            var panel = panels[2];
            panel.Clear();
            var scroll = CRTScroll();
            scroll.AddToClassList("orders-scroll");
            panel.Add(scroll);
            if (!incidentPending)
            {
                scroll.Add(Section("INCIDENTS"));
                scroll.Add(Line("No incidents. Too early to celebrate?"));
                if (!string.IsNullOrEmpty(lastIncidentLine)) scroll.Add(Clue(lastIncidentLine));
                return;
            }
            scroll.Add(Section("PENDING INCIDENT — clock paused"));
            foreach (var p in state.PendingIncidents)
            {
                var card = new VisualElement();
                card.AddToClassList("order-card");
                card.Add(Title(p.Incident.title));
                card.Add(Line(p.Incident.situationText));
                if (!string.IsNullOrEmpty(p.Incident.clueText)) card.Add(Clue(p.Incident.clueText));
                var ship = p.Context.Ship;
                if (ship != null)
                    card.Add(Line($"Ship {ship.DisplayName} — captain {CaptainName(ship.Captain)}"));
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
            Dirty();
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
            card.Add(CountdownLabel(o, "Due: ", $" · Revenue {o.Revenue}"));
            if (!string.IsNullOrEmpty(o.Clue)) card.Add(Clue(o.Clue));
            var row = new VisualElement();
            row.AddToClassList("btn-row");
            bool canAccept = CommandValidator.CanAccept(state, o.Id);
            var accept = new Button(() => { OrderActions.Accept(state, o.Id); Dirty(); }) { text = "ACCEPT" };
            accept.AddToClassList("btn-accept");
            accept.SetEnabled(canAccept);
            row.Add(accept);
            if (!canAccept)
            {
                var reason = Warn($"Already have {config.maxActiveOrders} active orders");
                row.Add(reason);
            }
            var decline = new Button(() => { OrderActions.Decline(state, o.Id); Dirty(); }) { text = "DECLINE" };
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
                if (s != null) ship = $" · ship {s.DisplayName}";
            }
            string status = o.Status == OrderStatus.Assigned ? "In transit" : "No ship";
            row.Add(CountdownLabel(o, $"{o.CustomerName} — {status}{ship} · {PortName(o.FromPortId)} → {PortName(o.ToPortId)} · Due: "));
            return row;
        }
        static IEnumerable<(OrderTag tag, string name)> CargoTags(OrderTag tags)
        {
            if ((tags & OrderTag.Fragile) != 0) yield return (OrderTag.Fragile, "[!] Fragile");
            if ((tags & OrderTag.Cold) != 0) yield return (OrderTag.Cold, "[*] Cold");
            if ((tags & OrderTag.Urgent) != 0) yield return (OrderTag.Urgent, "[>>] Urgent");
            if (tags == OrderTag.None) yield return (OrderTag.None, "Standard cargo");
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
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
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

