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

        string selectedShipId;
        string fleetError;
        string fleetMode = "assign"; // chế độ panel lệnh: "assign" | "reposition"

        static readonly Vector2[] IdleOffsets =
        {
            new Vector2(0f, 0f), new Vector2(0.05f, 0.04f),
            new Vector2(-0.05f, 0.04f), new Vector2(0.05f, -0.04f),
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

        // G5: chấm tàu Moving lẫn Roaming bám state mỗi frame; Idle đứng anchor cảng.
        void UpdateFleetDots()
        {
            if (state == null || shipDots == null || panels[1] == null || !panels[1].ClassListContains("on")) return;
            int index = 0;
            foreach (var s in state.Ships)
            {
                if (s.Status != ShipStatus.Idle && s.Route != null)
                {
                    if (shipDots.TryGetValue(s.Id, out var dot) && dot != null) PlaceDot(dot, s, index);
                }
                index++;
            }
        }

        // ---- dựng panel ------------------------------------------------------

        void BuildFleetPanel()
        {
            var panel = panels[1];
            panel.Clear();
            shipDots = new Dictionary<string, VisualElement>();
            shipEtaLabels.Clear(); // G5: đăng ký nhãn ETA tàu dựng lại từ đầu mỗi lần Refresh

            var row = new VisualElement();
            row.AddToClassList("fleet-row");
            var left = new VisualElement();
            left.AddToClassList("fleet-left");
            fleetMap = new VisualElement();
            fleetMap.AddToClassList("fleet-map");
            fleetMap.generateVisualContent += DrawFleetEdges;
            var legend = new VisualElement();
            legend.AddToClassList("fleet-legend");
            legend.pickingMode = PickingMode.Ignore; // R-01: click xuyên qua chú giải (USS không đặt được)
            var seen = new HashSet<HazardType>();
            foreach (var e in mapRouteDefs)
                if (seen.Add(e.hazard)) legend.Add(Chip("chip-h-" + e.hazard.ToString().ToLowerInvariant(), HazardLegend(e.hazard)));
            var side = CRTScroll(); // cột phải: MỘT ScrollView dọc duy nhất (thẻ tàu + panel lệnh)
            side.AddToClassList("fleet-side");
            row.Add(left);
            row.Add(side);
            left.Add(fleetMap);
            left.Add(legend);
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
                label.pickingMode = PickingMode.Ignore; // R-01: không chặn click nút hành tinh dưới label
                node.Add(label);
                node.tooltip = p.displayName;
                fleetMap.Add(node);
            }

            foreach (var s in state.Ships)
            {
                var dot = new VisualElement();
                dot.AddToClassList("map-dot");
                // G4: chữ cái đầu thuyền trưởng trong chấm — phân biệt tàu không cần màu.
                var letter = new Label(CaptainName(s.Captain)[0].ToString());
                letter.AddToClassList("map-dot-letter");
                letter.pickingMode = PickingMode.Ignore;
                dot.Add(letter);
                dot.tooltip = $"{s.DisplayName} — thuyền trưởng {s.Captain}";
                dot.RegisterCallback<ClickEvent>(_ => { selectedShipId = s.Id; Dirty(); });
                PlaceDot(dot, s, index);
                fleetMap.Add(dot);
                shipDots[s.Id] = dot;
                index++;
            }

            BuildShipCards(side);
            BuildCommandPanel(side);
        }

        // Chú giải hazard: tên + ký tự mẫu nét khớp DrawStyledLine (không phân biệt bằng màu).
        static string HazardLegend(HazardType h) => h switch
        {
            HazardType.Clear => "Clear ───",
            HazardType.Fog => "Fog ···",
            HazardType.Storm => "Storm ∿∿∿",
            HazardType.Pirates => "Pirates ━ ━",
            HazardType.Ice => "Ice ─·─",
            _ => h.ToString(),
        };

        void PlaceDot(VisualElement dot, ShipState ship, int shipIndex)
        {
            Vector2 pos = ship.Status != ShipStatus.Idle && ship.Route != null
                ? AlongRoute(ship.Route, ship.DistanceAlongRoute)
                : ShipAnchor(shipIndex, ship.PortId);
            SetDotPercent(dot, pos);
            // G4/G5: đơn sắc — Roaming mờ hơn Idle, không dùng màu mới.
            dot.EnableInClassList("roaming", ship.Status == ShipStatus.Roaming);
            if (ship.Id == selectedShipId) dot.AddToClassList("selected");
        }

        static void SetDotPercent(VisualElement dot, Vector2 pos)
        {
            dot.style.left = new Length(pos.x * 100f, LengthUnit.Percent);
            dot.style.top = new Length(pos.y * 100f, LengthUnit.Percent);
        }

        // ---- vẽ cạnh (generateVisualContent + Painter2D) ----------------------

        // G4 — cạnh đơn sắc: mọi hazard cùng màu --c-dim (khớp Terminal.uss), phân biệt bằng KIỂU NÉT:
        // Clear liền mảnh 2px · Fog chấm 2px cách 6px · Storm zigzag ±4px · Pirates đứt dày 3px (10/6)
        // · Ice gạch 8px cách 4px xen chấm 2px. Painter2D không có dash sẵn → tự chia đoạn.
        static readonly Color EdgeColor = new Color(95f / 255f, 150f / 255f, 105f / 255f);

        void DrawFleetEdges(MeshGenerationContext ctx)
        {
            if (mapRouteDefs == null) return;
            var rect = fleetMap.contentRect;
            var painter = ctx.painter2D;
            foreach (var edge in mapRouteDefs)
            {
                var a = PortPos(edge.fromPortId);
                var b = PortPos(edge.toPortId);
                DrawStyledLine(painter,
                    new Vector2(a.x * rect.width, a.y * rect.height),
                    new Vector2(b.x * rect.width, b.y * rect.height),
                    edge.hazard);
            }
        }

        static void DrawStyledLine(Painter2D p, Vector2 a, Vector2 b, HazardType h)
        {
            var dir = b - a;
            float len = dir.magnitude;
            if (len < 1f) return;
            dir /= len;
            var n = new Vector2(-dir.y, dir.x); // pháp tuyến — cho zigzag Storm
            switch (h)
            {
                case HazardType.Fog: // chấm: đoạn ngắn 2px vẽ dày thành chấm tròn, cách 6px
                    Dashes(p, a, dir, len, start: 0f, dash: 2f, gap: 6f, width: 4f);
                    break;
                case HazardType.Storm: // zigzag: điểm lệch ±4px xen kẽ mỗi 12px
                    p.strokeColor = EdgeColor;
                    p.lineWidth = 2f;
                    p.BeginPath();
                    p.MoveTo(a);
                    for (float t = 12f; t < len; t += 12f)
                        p.LineTo(a + dir * t + n * (((int)(t / 12f) & 1) == 1 ? 4f : -4f));
                    p.LineTo(b);
                    p.ClosePath();
                    break;
                case HazardType.Pirates: // đứt dày: đoạn 10px, cách 6px
                    Dashes(p, a, dir, len, start: 0f, dash: 10f, gap: 6f, width: 3f);
                    break;
                case HazardType.Ice: // gạch 8px cách 4px, chấm 2px giữa mỗi khoảng trống
                    Dashes(p, a, dir, len, start: 0f, dash: 8f, gap: 4f, width: 2f);
                    Dashes(p, a, dir, len, start: 9f, dash: 2f, gap: 10f, width: 4f);
                    break;
                default: // Clear: nét liền mảnh
                    p.strokeColor = EdgeColor;
                    p.lineWidth = 2f;
                    p.BeginPath();
                    p.MoveTo(a);
                    p.LineTo(b);
                    p.ClosePath();
                    break;
            }
        }

        // Vẽ các đoạn nét đều dọc cạnh (dash/gap), mỗi lần gọi là một đường riêng.
        static void Dashes(Painter2D p, Vector2 a, Vector2 dir, float len, float start, float dash, float gap, float width)
        {
            p.strokeColor = EdgeColor;
            p.lineWidth = width;
            p.BeginPath();
            for (float t = start; t < len; t += dash + gap)
            {
                p.MoveTo(a + dir * t);
                p.LineTo(a + dir * Mathf.Min(t + dash, len));
            }
            p.ClosePath();
        }

        // ---- cột phải: thẻ tàu ----------------------------------------------

        void BuildShipCards(VisualElement side)
        {
            side.Add(Section("ĐỘI TÀU"));
            int index = 0;
            foreach (var s in state.Ships)
            {
                var card = new VisualElement();
                card.AddToClassList("ship-card");
                if (s.Id == selectedShipId) card.AddToClassList("selected");
                card.RegisterCallback<ClickEvent>(_ => { selectedShipId = s.Id; Dirty(); });
                card.Add(Title(s.DisplayName));
                card.Add(Line($"Thuyền trưởng: {CaptainName(s.Captain)} · {SpeedName(s.Speed)} · {ConditionName(s.Condition)}"));
                var def = shipDefs != null ? shipDefs.Find(d => d.shipId == s.Id) : null;
                if (def != null && !string.IsNullOrEmpty(def.traits)) card.Add(Line(def.traits));
                if (s.Status == ShipStatus.Roaming && s.Route != null)
                {
                    // G5: số giây tới cảng cập tại chỗ ở nhịp 10 Hz (shipEtaLabels) — không dựng lại panel mỗi frame.
                    var loc = new Label("");
                    loc.AddToClassList("order-line");
                    card.Add(loc);
                    shipEtaLabels.Add((s, loc, LocationPrefix(s), "s"));
                }
                else card.Add(Line(LocationLine(s, index)));
                card.Add(Line(CargoLine(s)));
                side.Add(card);
                index++;
            }
        }

        string LocationPrefix(ShipState s)
            => $"Đang đi {PortName(s.Route.portPath[0])} → {PortName(s.Route.portPath[s.Route.portPath.Count - 1])}, tới sau ";

        const string RoamWaitPrefix = "Tàu đang đi, chỉ ra lệnh được khi cập cảng (tới sau ";

        string LocationLine(ShipState s, int shipIndex)
        {
            if (s.Status != ShipStatus.Idle && s.Route != null)
            {
                float pct = s.Route.length > 0.0001f ? s.DistanceAlongRoute / s.Route.length * 100f : 0f;
                return $"Đang đi {PortName(s.Route.portPath[0])} → {PortName(s.Route.portPath[s.Route.portPath.Count - 1])} ({pct:0}%)";
            }
            return $"Đang đậu ở {PortName(s.PortId)}";
        }

        string CargoLine(ShipState s)
        {
            if (string.IsNullOrEmpty(s.OrderId)) return "Không chở hàng";
            var o = state.Orders.Find(x => x.Id == s.OrderId);
            return o != null ? $"Chở: {o.CustomerName} → {PortName(o.ToPortId)}" : "Không chở hàng";
        }

        float EtaSeconds(RouteOption route, ShipState ship)
        {
            if (route == null || ship == null || route.length <= 0.0001f) return 0f;
            return route.length / config.SpeedUnitsPerSecond(ship.Speed); // giây — đồng hồ chạy thật (G1)
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

            // Hai nút chuyển chế độ rõ ràng (mặc định GIAO ĐƠN).
            var modes = new VisualElement();
            modes.AddToClassList("btn-row");
            var btnAssign = new Button(() => { fleetMode = "assign"; fleetError = null; Dirty(); }) { text = "GIAO ĐƠN" };
            btnAssign.AddToClassList("btn-mode");
            if (fleetMode == "assign") btnAssign.AddToClassList("selected");
            var btnRepos = new Button(() => { fleetMode = "reposition"; fleetError = null; Dirty(); }) { text = "ĐIỀU TÀU RỖNG" };
            btnRepos.AddToClassList("btn-mode");
            if (fleetMode == "reposition") btnRepos.AddToClassList("selected");
            modes.Add(btnAssign);
            modes.Add(btnRepos);
            area.Add(modes);

            if (fleetError != null)
            {
                area.Add(Warn(fleetError));
            }

            var ship = state.Ships.Find(s => s.Id == selectedShipId);
            if (ship == null)
            {
                area.Add(Line("Chọn một tàu để ra lệnh."));
                side.Add(area);
                return;
            }

            // Tàu đang chạy thì chỉ xem trên bản đồ (áp ngay nên không có pha "chờ MAKE IT SO").
            if (ship.Status == ShipStatus.Roaming && ship.Route != null)
            {
                // G5: tàu Roaming không nhận lệnh giữa cạnh — số giây cập 10 Hz tại chỗ.
                var wait = new Label("");
                wait.AddToClassList("order-line");
                area.Add(wait);
                shipEtaLabels.Add((ship, wait, RoamWaitPrefix, "s)"));
                side.Add(area);
                return;
            }
            if (ship.Status != ShipStatus.Idle)
            {
                area.Add(Line("Tàu đang chạy — xem trên bản đồ."));
                side.Add(area);
                return;
            }

            if (fleetMode == "assign") BuildAssignArea(area, ship);
            else BuildRepositionArea(area, ship);
            side.Add(area);
        }

        void BuildAssignArea(VisualElement area, ShipState ship)
        {
            var orders = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted
                && string.IsNullOrEmpty(o.AssignedShipId) && o.FromPortId == ship.PortId);
            if (orders.Count == 0)
            {
                var none = new Label("Không có đơn đã nhận nào phù hợp cảng đi của tàu.");
                none.AddToClassList("order-line");
                none.AddToClassList("order-muted");
                area.Add(none);
                return;
            }
            foreach (var o in orders)
            {
                var card = new VisualElement();
                card.AddToClassList("order-card");
                card.Add(Title(o.CustomerName));
                card.Add(Line($"{PortName(o.FromPortId)} → {PortName(o.ToPortId)} · Doanh thu {o.Revenue}"));
                card.Add(CountdownLabel(o, "Hạn: "));

                var options = routeTable.GetOptions(o.FromPortId, o.ToPortId);
                if (options == null || options.Count == 0) { card.Add(Line("Không có tuyến đường.")); area.Add(card); continue; }

                var optRow = new VisualElement();
                optRow.AddToClassList("btn-row");
                for (int i = 0; i < options.Count; i++)
                {
                    int idx = i;
                    var opt = options[i];
                    // MAKE IT SO riêng từng thẻ lệnh (G2.3): bấm tuyến = tàu khởi hành ngay.
                    var btn = new Button(() =>
                    {
                        fleetError = FleetActions.Assign(state, routeTable, ship.Id, o.Id, idx);
                        Dirty();
                    }) { text = $"{opt.label} · {opt.length:0.#} · {EtaSeconds(opt, ship):0}s" };
                    btn.AddToClassList("btn-route");
                    optRow.Add(btn);
                }
                card.Add(optRow);
                area.Add(card);
            }
        }

        void BuildRepositionArea(VisualElement area, ShipState ship)
        {
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
                        Dirty();
                    }) { text = $"→ {kv.Value.displayName} · {opt.label} · {EtaSeconds(opt, ship):0}s" };
                    btn.AddToClassList("btn-route");
                    row.Add(btn);
                    any = true;
                }
            }
            if (!any) row.Add(Line("Không có tuyến đi rỗng từ cảng này."));
            area.Add(row);
        }
    }
}
