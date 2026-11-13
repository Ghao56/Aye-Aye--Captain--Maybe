using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AyeAye
{
    // G3 — tab ĐỘI TÀU (FR-04xx): bản đồ hình sao + thẻ tàu + panel lệnh.
    // Render-only: đọc state + config + RouteTable, phát intent qua FleetActions; animation chỉ ở UI.
    public partial class TerminalHud
    {
        Dictionary<string, VisualElement> shipDots;      // shipId → chấm tàu trên bản đồ (dựng lại mỗi Refresh)
        VisualElement fleetMap;
        bool fleetAnimating;
        float fleetElapsed;
        Dictionary<string, (RouteOption route, float distance, string portId)> pendingSimSnapshot;

        string selectedShipId;
        string pendingOrderId;      // đơn đang chọn tuyến trong panel lệnh
        int pendingOptionIndex = -1;
        RouteOption previewRoute;   // tuyến tô sáng trên bản đồ (chọn trước khi staged, hoặc lệnh đã staged)
        string fleetError;

        static readonly Vector2[] IdleOffsets =
        {
            new Vector2(0f, 0f), new Vector2(0.025f, 0.025f),
            new Vector2(-0.025f, 0.02f), new Vector2(0.02f, -0.025f),
        };

        // ---- dữ liệu vị trí -------------------------------------------------

        Vector2 PortPos(string portId)
            => portDefs != null && portDefs.TryGetValue(portId, out var p) ? p.mapPosition : new Vector2(0.5f, 0.5f);

        Vector2 AlongRoute(RouteOption route, float distance)
        {
            int seg;
            float t = RouteProgress.SegmentAt(route, distance, out seg);
            var a = PortPos(route.portPath[seg]);
            var b = PortPos(route.portPath[seg + 1]);
            return a + (b - a) * t;
        }

        Vector2 ShipAnchor(int shipIndex, string portId) => PortPos(portId) + IdleOffsets[shipIndex % IdleOffsets.Length];

        Dictionary<string, (RouteOption, float, string)> CaptureSimSnapshot()
        {
            var snap = new Dictionary<string, (RouteOption, float, string)>();
            foreach (var s in state.Ships)
                snap[s.Id] = (s.Route, s.DistanceAlongRoute, s.PortId);
            return snap;
        }

        // ---- dựng panel ------------------------------------------------------

        void BuildFleetPanel()
        {
            StopFleetAnim();
            var panel = panels[1];
            panel.Clear();
            shipDots = new Dictionary<string, VisualElement>();

            var row = new VisualElement();
            row.AddToClassList("fleet-row");
            fleetMap = new VisualElement();
            fleetMap.AddToClassList("fleet-map");
            fleetMap.generateVisualContent += DrawFleetEdges;
            fleetMap.RegisterCallback<MouseDownEvent>(_ => SkipFleetAnim()); // SKIP: bấm vào bản đồ
            var side = new VisualElement();
            side.AddToClassList("fleet-side");
            row.Add(fleetMap);
            row.Add(side);
            panel.Add(row);

            // Nút hành tinh + chấm tàu (tọa độ %, tự co giãn theo kích thước phần tử)
            int index = 0;
            foreach (var kv in portDefs)
            {
                var p = kv.Value;
                var node = new VisualElement();
                node.AddToClassList("map-node");
                node.AddToClassList(p.isHeadquarters ? "map-hq" : "map-planet");
                node.style.left = new Length(p.mapPosition.x * 100f, LengthUnit.Percent);
                node.style.top = new Length(p.mapPosition.y * 100f, LengthUnit.Percent);
                var label = new Label(p.displayName);
                label.AddToClassList("map-node-label");
                node.Add(label);
                node.tooltip = p.displayName;
                fleetMap.Add(node);
            }

            // Chú giải màu nguy hiểm (từ RouteDef — nguồn duy nhất là dữ liệu mẫu).
            var legend = new VisualElement();
            legend.AddToClassList("map-legend");
            var seen = new HashSet<HazardType>();
            foreach (var e in mapRouteDefs)
                if (seen.Add(e.hazard)) legend.Add(Chip("chip-h-" + e.hazard.ToString().ToLowerInvariant(), e.hazard.ToString()));
            fleetMap.Add(legend);

            foreach (var s in state.Ships)
            {
                var dot = new VisualElement();
                dot.AddToClassList("map-dot");
                dot.AddToClassList("dot-" + (index % IdleOffsets.Length));
                dot.tooltip = $"{s.DisplayName} — thuyền trưởng {s.Captain}";
                dot.RegisterCallback<MouseDownEvent>(_ => { selectedShipId = s.Id; Dirty(); });
                PlaceDot(dot, s, index);
                fleetMap.Add(dot);
                shipDots[s.Id] = dot;
                index++;
            }

            BuildShipCards(side);
            BuildCommandPanel(side);
        }

        void PlaceDot(VisualElement dot, ShipState ship, int shipIndex)
        {
            Vector2 pos = ship.Status == ShipStatus.Moving && ship.Route != null
                ? AlongRoute(ship.Route, ship.DistanceAlongRoute)
                : ShipAnchor(shipIndex, ship.PortId);
            SetDotPercent(dot, pos);
            if (ship.Id == selectedShipId) dot.AddToClassList("selected");
        }

        static void SetDotPercent(VisualElement dot, Vector2 pos)
        {
            dot.style.left = new Length(pos.x * 100f, LengthUnit.Percent);
            dot.style.top = new Length(pos.y * 100f, LengthUnit.Percent);
        }

        // ---- vẽ cạnh (generateVisualContent + Painter2D) ----------------------

        // shortcut: màu cạnh khớp biến màu trong Terminal.uss — đổi màu phải sửa cả hai nơi.
        static Color HazardColor(HazardType h) => h switch
        {
            HazardType.Fog => new Color(0.62f, 0.71f, 0.79f),
            HazardType.Storm => new Color(0.85f, 0.63f, 0.29f),
            HazardType.Pirates => new Color(0.75f, 0.36f, 0.30f),
            HazardType.Ice => new Color(0.44f, 0.72f, 0.79f),
            _ => new Color(0.42f, 0.48f, 0.35f), // Clear
        };

        static float HazardWidth(HazardType h) => h == HazardType.Clear ? 2f : 3f;

        void DrawFleetEdges(MeshGenerationContext ctx)
        {
            if (mapRouteDefs == null) return;
            var rect = fleetMap.contentRect;
            var painter = ctx.painter2D;
            foreach (var edge in mapRouteDefs)
            {
                var a = PortPos(edge.fromPortId);
                var b = PortPos(edge.toPortId);
                painter.strokeColor = HazardColor(edge.hazard);
                painter.lineWidth = HazardWidth(edge.hazard);
                painter.BeginPath();
                painter.MoveTo(new Vector2(a.x * rect.width, a.y * rect.height));
                painter.LineTo(new Vector2(b.x * rect.width, b.y * rect.height));
                painter.ClosePath();
            }
            // Tuyến đang xem trước (chọn lệnh hoặc lệnh đã staged): nét đứt màu hổ phách.
            if (previewRoute != null && previewRoute.portPath != null && previewRoute.portPath.Count > 1)
            {
                painter.strokeColor = new Color(0.95f, 0.78f, 0.35f);
                painter.lineWidth = 2.5f;
                painter.dashPattern = new float[] { 5f, 4f };
                painter.BeginPath();
                painter.MoveTo(new Vector2(PortPos(previewRoute.portPath[0]).x * rect.width,
                                           PortPos(previewRoute.portPath[0]).y * rect.height));
                for (int i = 1; i < previewRoute.portPath.Count; i++)
                {
                    var p = PortPos(previewRoute.portPath[i]);
                    painter.LineTo(new Vector2(p.x * rect.width, p.y * rect.height));
                }
                painter.ClosePath();
            }
        }

        // ---- cột phải: thẻ tàu ----------------------------------------------

        void BuildShipCards(VisualElement side)
        {
            var cards = new ScrollView();
            cards.AddToClassList("fleet-cards");
            cards.Add(Section("ĐỘI TÀU"));
            int index = 0;
            foreach (var s in state.Ships)
            {
                var card = new VisualElement();
                card.AddToClassList("ship-card");
                if (s.Id == selectedShipId) card.AddToClassList("selected");
                card.RegisterCallback<MouseDownEvent>(_ => { selectedShipId = s.Id; pendingOrderId = null; previewRoute = null; Dirty(); });
                card.Add(Title(s.DisplayName));
                card.Add(Line($"Thuyền trưởng {CaptainName(s.Captain)} · Tốc độ {SpeedName(s.Speed)} · {ConditionName(s.Condition)}"));
                card.Add(Line(LocationLine(s, index)));
                card.Add(Line(CargoLine(s)));
                var cmd = state.StagedCommands.Find(c => c.ShipId == s.Id);
                if (cmd != null) card.Add(Line(StagedLine(cmd)));
                cards.Add(card);
                index++;
            }
            side.Add(cards);
        }

        string LocationLine(ShipState s, int shipIndex)
        {
            if (s.Status == ShipStatus.Moving && s.Route != null)
            {
                float pct = s.Route.length > 0.0001f ? s.DistanceAlongRoute / s.Route.length * 100f : 0f;
                return $"Đang đi {PortName(s.Route.portPath[0])} → {PortName(s.Route.portPath[s.Route.portPath.Count - 1])} ({pct:0}%)";
            }
            return $"Đang ở {PortName(s.PortId)}";
        }

        string CargoLine(ShipState s)
        {
            if (string.IsNullOrEmpty(s.OrderId)) return "Không chở hàng";
            var o = state.Orders.Find(x => x.Id == s.OrderId);
            return o != null ? $"Chở: {o.CustomerName} → {PortName(o.ToPortId)}" : "Không chở hàng";
        }

        string StagedLine(StagedCommand cmd)
        {
            string desc = cmd.Kind == CommandKind.Assign && !string.IsNullOrEmpty(cmd.OrderId)
                ? $"Giao đơn {cmd.OrderId}"
                : $"Điều phóng rỗng → {PortName(cmd.Route.ToPort)}";
            float eta = EtaDays(cmd.Route, state.Ships.Find(s => s.Id == cmd.ShipId));
            return $"Lệnh (chờ MAKE IT SO): {desc} · {cmd.Route.label} · ETA {eta:0.#} ngày";
        }

        float EtaDays(RouteOption route, ShipState ship)
        {
            if (route == null || ship == null || route.length <= 0.0001f) return 0f;
            return Mathf.Ceil(route.length / config.SpeedValue(ship.Speed) - 0.0001f);
        }

        static string CaptainName(CrewId c) => c switch
        {
            CrewId.Bob => "Bob", CrewId.Mira => "Mira", CrewId.Chen => "Chen", CrewId.Dex => "Dex",
            _ => c.ToString(),
        };

        static string SpeedName(SpeedClass s) => s switch
        {
            SpeedClass.Fast => "Nhanh", SpeedClass.Slow => "Chậm", _ => "Thường",
        };

        static string ConditionName(ShipCondition c) => c == ShipCondition.Good ? "Tốt" : "Mòn";

        // ---- panel lệnh ------------------------------------------------------

        void BuildCommandPanel(VisualElement side)
        {
            var area = new VisualElement();
            area.AddToClassList("fleet-commands");
            area.Add(Section("LỆNH"));

            if (fleetError != null)
            {
                var err = new Label(fleetError);
                err.AddToClassList("order-line");
                err.AddToClassList("order-warn");
                area.Add(err);
            }

            var ship = state.Ships.Find(s => s.Id == selectedShipId);
            if (ship == null)
            {
                area.Add(Line("Chọn một tàu để ra lệnh."));
                side.Add(area);
                return;
            }

            var staged = state.StagedCommands.Find(c => c.ShipId == ship.Id);
            if (staged != null)
            {
                previewRoute = staged.Route;
                area.Add(Line(StagedLine(staged)));
                var cancel = new Button(() =>
                {
                    fleetError = FleetActions.Cancel(state, ship.Id);
                    pendingOrderId = null;
                    if (fleetError == null) previewRoute = null;
                    Dirty();
                }) { text = "HỦY LỆNH" };
                cancel.AddToClassList("btn-decline");
                area.Add(cancel);
                side.Add(area);
                return;
            }

            // Chỉ ra lệnh được trong pha Lập kế hoạch (PhaseGate.Interactive).
            if (PhaseGate.TabAccess(state.Phase, 1) != TabAccess.Interactive || ship.Status != ShipStatus.Idle)
            {
                area.Add(Line(state.Phase == GamePhase.Simulation
                    ? "Tàu đang chạy — xem trên bản đồ."
                    : "Không ra lệnh được ở pha này."));
                side.Add(area);
                return;
            }

            BuildAssignArea(area, ship);
            BuildRepositionArea(area, ship);
            side.Add(area);
        }

        void BuildAssignArea(VisualElement area, ShipState ship)
        {
            area.Add(Section("GIAO ĐƠN"));
            var orders = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted
                && string.IsNullOrEmpty(o.AssignedShipId) && o.FromPortId == ship.PortId);
            if (orders.Count == 0)
            {
                area.Add(Line("Không có đơn đã nhận nào phù hợp cảng đi của tàu."));
                return;
            }
            foreach (var o in orders)
            {
                var card = new VisualElement();
                card.AddToClassList("order-card");
                card.Add(Title(o.CustomerName));
                card.Add(Line($"{PortName(o.FromPortId)} → {PortName(o.ToPortId)} · Doanh thu {o.Revenue} · Hạn: Ngày {o.DeadlineDay}"));

                var options = routeTable.GetOptions(o.FromPortId, o.ToPortId);
                if (options == null || options.Count == 0) { card.Add(Line("Không có tuyến đường.")); area.Add(card); continue; }

                var optRow = new VisualElement();
                optRow.AddToClassList("btn-row");
                for (int i = 0; i < options.Count; i++)
                {
                    int idx = i;
                    var opt = options[i];
                    var btn = new Button(() =>
                    {
                        pendingOrderId = o.Id;
                        pendingOptionIndex = idx;
                        previewRoute = opt;
                        fleetError = null;
                        Dirty();
                    }) { text = $"{opt.label} · {opt.length:0.#} ({EtaDays(opt, ship):0.#} ngày)" };
                    btn.AddToClassList("btn-route");
                    if (pendingOrderId == o.Id && pendingOptionIndex == idx) btn.AddToClassList("selected");
                    optRow.Add(btn);
                }
                card.Add(optRow);

                if (pendingOrderId == o.Id && pendingOptionIndex >= 0)
                {
                    var confirm = new Button(() =>
                    {
                        fleetError = FleetActions.Assign(state, routeTable, ship.Id, o.Id, pendingOptionIndex);
                        pendingOrderId = null;
                        pendingOptionIndex = -1;
                        if (fleetError == null) previewRoute = null; // lệnh đã staged → preview giữ qua staged.Route
                        Dirty();
                    }) { text = "XÁC NHẬN LỆNH" };
                    confirm.AddToClassList("btn-accept");
                    card.Add(confirm);
                }
                area.Add(card);
            }
        }

        void BuildRepositionArea(VisualElement area, ShipState ship)
        {
            area.Add(Section("ĐIỀU TÀU RỖNG"));
            var row = new VisualElement();
            row.AddToClassList("btn-row");
            bool any = false;
            foreach (var kv in portDefs)
            {
                if (kv.Key == ship.PortId) continue;
                var options = routeTable.GetOptions(ship.PortId, kv.Key);
                if (options == null) continue;
                for (int i = 0; i < options.Count; i++)
                {
                    int idx = i;
                    var opt = options[i];
                    var btn = new Button(() =>
                    {
                        fleetError = FleetActions.Reposition(state, routeTable, ship.Id, kv.Key, idx);
                        if (fleetError == null) previewRoute = null;
                        Dirty();
                    }) { text = $"→ {kv.Value.displayName} · {opt.label} ({EtaDays(opt, ship):0.#} ngày)" };
                    btn.AddToClassList("btn-route");
                    row.Add(btn);
                    any = true;
                }
            }
            if (!any) row.Add(Line("Không có tuyến đi rỗng từ cảng này."));
            area.Add(row);
        }

        // ---- animation pha Simulation (chỉ ở UI; không đổi state/Rng) --------

        // shortcut: pha Incident hiện sự cố sau animation phải dây ở Bước 5 (hiện chưa có UI sự cố tương tác).
        void StartFleetAnimationIfPending()
        {
            if (pendingSimSnapshot == null || fleetAnimating) return;
            if (state.Phase != GamePhase.Simulation) { pendingSimSnapshot = null; return; }
            var simSnapshot = pendingSimSnapshot;

            var moves = new List<(string shipId, Vector2 from, Vector2 to)>();
            int index = 0;
            foreach (var s in state.Ships)
            {
                Vector2 end = s.Status == ShipStatus.Moving && s.Route != null
                    ? AlongRoute(s.Route, s.DistanceAlongRoute)
                    : ShipAnchor(index, s.PortId);
                Vector2 start = end;
                if (simSnapshot.TryGetValue(s.Id, out var snap))
                    start = snap.Item1 != null ? AlongRoute(snap.Item1, snap.Item2) : ShipAnchor(index, snap.Item3);
                if ((end - start).sqrMagnitude > 0.000001f)
                    moves.Add((s.Id, start, end));
                index++;
            }
            pendingSimSnapshot = null;
            if (moves.Count == 0) return;

            int duration = Mathf.Max(200, config.fleetAnimMs);
            fleetAnimating = true;
            foreach (var t in tabs) t.SetEnabled(false);
            bottomButton.SetEnabled(false);

            fleetMap.schedule.Execute(() =>
            {
                if (!fleetAnimating) return;
                fleetElapsed += 16f;
                float k = Mathf.Clamp01(fleetElapsed / duration);
                float eased = k * k * (3f - 2f * k); // smoothstep
                foreach (var (shipId, from, to) in moves)
                {
                    if (!shipDots.TryGetValue(shipId, out var dot) || dot == null) continue;
                    if (k >= 1f) SetDotPercent(dot, to);
                    else SetDotPercent(dot, from + (to - from) * eased);
                }
                if (k >= 1f) FinishFleetAnim();
            }).Every(16);
        }

        void FinishFleetAnim()
        {
            fleetAnimating = false;
            foreach (var t in tabs) t.SetEnabled(true);
            bottomButton.SetEnabled(true);
        }

        void SkipFleetAnim()
        {
            if (!fleetAnimating) return;
            fleetElapsed = float.MaxValue / 2f; // tick kế → k=1 → kết thúc
        }

        void StopFleetAnim()
        {
            if (fleetAnimating) FinishFleetAnim();
        }
    }
}
