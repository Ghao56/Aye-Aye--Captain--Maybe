using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AyeAye
{
    // G3+G6.3 — tab ĐỘI TÀU (FR-04xx): hai cột. Trái = bản đồ (min 300px) + đơn chờ điều tàu
    // (thẻ 2 dòng: khách → đích / hạn+doanh thu+chip, ~2 thẻ trong khung, dư thì cuộn).
    // Phải = MỘT ScrollView duy nhất chứa thẻ tàu + panel lệnh TỐI GIẢN:
    // 1 dòng hướng dẫn theo trạng thái + tối đa 2 nút tuyến TO (bấm = XUẤT PHÁT NGAY qua
    // FleetActions.Assign); điều rỗng là nút phụ nhỏ, chỉ hiện khi đã chọn tàu rảnh.
    // Lý do bố cục: kính thật chỉ ~1207x679 px thiết kế (zoomOutFactor 1.27 + scale máy 0.22 —
    // TerminalView.FitScreenToFrame). Hết chỗ → CUỘN trong vùng của mình, không vẽ đè vùng khác.
    // Số động (ETA/%, hạn đơn) sửa text TẠI CHỖ 10 Hz (shipEtaLabels/countLabels) — tàu Moving
    // cũng có ETA tại chỗ, không cần bấm mới cập nhật. Chọn đôi: đơn → tàu (tự chọn nếu chỉ 1)
    // → nút tuyến. Lựa chọn chỉ là state tạm — lệnh vẫn qua FleetActions, áp NGAY (không staged).
    // Chữ trong thẻ dùng nhãn cảng ngắn + tooltip đầy đủ (portId gốc không đổi).
    public partial class TerminalHud
    {
        Dictionary<string, VisualElement> shipDots;      // shipId → chấm tàu trên bản đồ (dựng lại mỗi Refresh)
        VisualElement fleetMap;

        string selectedShipId;
        string selectedOrderId;
        string selectedReposPortId;  // cảng đích điều rỗng (bấm nút hành tinh trên bản đồ)
        string fleetError;
        string fleetFeedback;        // thông báo lệnh vừa thành công
        string fleetMode = "assign"; // "assign" | "reposition" — điều rỗng vào bằng nút phụ nhỏ (G6.3)

        static readonly Vector2[] IdleOffsets =
        {
            new Vector2(0f, 0f), new Vector2(0.05f, 0.04f),
            new Vector2(-0.05f, 0.04f), new Vector2(0.05f, -0.04f),
        };

        // ---- lựa chọn tạm (không đụng simulation) -----------------------------

        // Đơn đang chọn — chỉ nhận đơn ĐÃ NHẬN chưa có tàu; nếu state đã đổi thì coi như bỏ chọn.
        OrderState SelectedOrder()
        {
            if (string.IsNullOrEmpty(selectedOrderId)) return null;
            var o = state.Orders.Find(x => x.Id == selectedOrderId);
            return o != null && o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId) ? o : null;
        }

        void SelectShip(string shipId)
        {
            selectedShipId = shipId;
            selectedReposPortId = null;
            fleetError = null;
            fleetFeedback = null;
            Dirty();
        }

        void SelectOrder(string orderId)
        {
            selectedOrderId = orderId;
            fleetMode = "assign"; // bấm đơn = về luồng giao đơn (thoát điều rỗng)
            selectedReposPortId = null;
            fleetError = null;
            fleetFeedback = null;
            // G6.3: chỉ đúng 1 tàu đủ điều kiện (rảnh + đậu đúng cảng đi) → TỰ CHỌN, bỏ một bước bấm.
            var o = SelectedOrder();
            if (o != null)
            {
                var fits = state.Ships.FindAll(s => s.Status == ShipStatus.Idle && s.PortId == o.FromPortId);
                if (fits.Count == 1) selectedShipId = fits[0].Id;
            }
            Dirty();
        }

        // ---- nhãn hiển thị ----------------------------------------------------

        // Nhãn ngắn cho chỗ hẹp (thẻ, tóm tắt lệnh): bỏ tiền tố đầu ("Trạm", "Phố", "Đảo"...).
        // Tên đầy đủ vẫn ở tooltip — portId/portDef không bị thay đổi.
        string ShortPortName(string portId)
        {
            var full = PortName(portId);
            int sp = full.IndexOf(' ');
            return sp > 0 && sp < full.Length - 1 ? full.Substring(sp + 1).ToUpperInvariant() : full.ToUpperInvariant();
        }

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
            // G6.2: giữ vị trí cuộn qua lần dựng lại — đọc trước khi Clear.
            float savedSide = SavedScroll(panel, "fleet-side-scroll");
            float savedOrders = SavedScroll(panel, "fleet-orders-scroll");
            panel.Clear();
            shipDots = new Dictionary<string, VisualElement>();
            shipEtaLabels.Clear(); // G5: đăng ký nhãn ETA tàu dựng lại từ đầu mỗi lần Refresh

            // Hai cột: trái = bản đồ + đơn chờ dưới bản đồ; phải = MỘT ScrollView (thẻ tàu + thanh lệnh).
            var row = new VisualElement();
            row.AddToClassList("fleet-row");

            var left = new VisualElement();
            left.AddToClassList("fleet-left");
            fleetMap = new VisualElement();
            fleetMap.AddToClassList("fleet-map");
            fleetMap.generateVisualContent += DrawFleetEdges;
            left.Add(fleetMap);

            var side = new VisualElement();
            side.AddToClassList("fleet-side");
            var sideScroll = CRTScroll();
            sideScroll.AddToClassList("fleet-side-scroll");
            side.Add(sideScroll);

            var grid = new VisualElement();
            grid.AddToClassList("ship-grid");

            row.Add(left);
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
                label.pickingMode = PickingMode.Ignore; // R-01: không chặn click nút hành tinh dưới label
                node.Add(label);
                node.tooltip = p.displayName;
                // Bấm cảng khi đang chọn tàu đậu → chuyển sang điều rỗng tới cảng đó (dùng validator sẵn có).
                var portId = kv.Key;
                node.RegisterCallback<ClickEvent>(_ =>
                {
                    var s = state.Ships.Find(x => x.Id == selectedShipId);
                    if (s != null && s.Status == ShipStatus.Idle && portId != s.PortId)
                    {
                        fleetMode = "reposition";
                        selectedReposPortId = portId;
                        fleetError = null;
                        fleetFeedback = null;
                        Dirty();
                    }
                });
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
                dot.RegisterCallback<ClickEvent>(_ => SelectShip(s.Id));
                PlaceDot(dot, s, index);
                // G6.3: đơn đang chọn → mờ chấm tàu không đủ điều kiện (thẻ tàu cũng mờ như cũ).
                var selOrder = SelectedOrder();
                if (selOrder != null && !(s.Status == ShipStatus.Idle && s.PortId == selOrder.FromPortId))
                    dot.AddToClassList("dimmed");
                fleetMap.Add(dot);
                shipDots[s.Id] = dot;
                index++;
            }

            int cardIndex = 0;
            foreach (var s in state.Ships)
            {
                grid.Add(ShipCard(s, cardIndex));
                cardIndex++;
            }

            // Đơn chờ: nằm dưới bản đồ trong cột trái; cột phải: thẻ tàu + thanh lệnh trong 1 ScrollView.
            left.Add(BuildOrdersRow(savedOrders));
            sideScroll.Add(Section("ĐỘI TÀU"));
            sideScroll.Add(grid);
            sideScroll.Add(BuildCommandBar());

            sideScroll.schedule.Execute(() => sideScroll.scrollOffset = new Vector2(0, savedSide)).ExecuteLater(1);
        }

        // Vị trí cuộn cũ của ScrollView theo class (0 nếu chưa có) — gọi TRƯỚC panel.Clear().
        static float SavedScroll(VisualElement panel, string className)
            => panel.Q(className: className) is ScrollView s ? s.scrollOffset.y : 0f;

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

        // ---- vùng B: thẻ tàu 2x2 ----------------------------------------------

        static string StatusName(ShipStatus s) => s switch
        {
            ShipStatus.Idle => "RỖNH",
            ShipStatus.Roaming => "TỰ ĐI",
            _ => "ĐANG CHẠY",
        };

        // Thẻ tàu gọn: tiêu đề + 2 dòng thông tin. Chi tiết (tính cách, tên cảng đầy đủ) ở tooltip.
        VisualElement ShipCard(ShipState s, int shipIndex)
        {
            var card = new VisualElement();
            card.AddToClassList("ship-card");
            if (s.Id == selectedShipId) card.AddToClassList("selected");
            var selOrder = SelectedOrder();
            // Đơn đang chọn → tô tàu nhận được (đậu đúng cảng đi), mờ tàu không khả dụng.
            if (selOrder != null)
                card.AddToClassList(s.Status == ShipStatus.Idle && s.PortId == selOrder.FromPortId ? "compatible" : "dimmed");
            var def = shipDefs != null ? shipDefs.Find(d => d.shipId == s.Id) : null;
            card.tooltip = ShipCardTooltip(s, def);

            var head = new VisualElement();
            head.AddToClassList("ship-head");
            var name = new Label(s.DisplayName);
            name.AddToClassList("order-title");
            var status = new Label(StatusName(s.Status));
            status.AddToClassList("ship-status");
            head.Add(name);
            head.Add(status);
            card.Add(head);
            card.Add(Line($"{CaptainName(s.Captain)} · {SpeedName(s.Speed)} · {ConditionName(s.Condition)}"));

            if (s.Status != ShipStatus.Idle && s.Route != null)
            {
                // G6.3: Roaming lẫn Moving đều sửa text TẠI CHỖ nhịp 10 Hz (shipEtaLabels) —
                // không dựng lại panel mỗi frame, không cần bấm thẻ mới cập nhật (Việc 1).
                var loc = new Label("");
                loc.AddToClassList("order-line");
                card.Add(loc);
                string cargo = string.IsNullOrEmpty(s.OrderId) ? "" : $" · CHỞ {s.OrderId}";
                if (s.Status == ShipStatus.Roaming)
                    shipEtaLabels.Add((s, loc, $"TỰ ĐI · Tới {ShortPortName(s.Route.ToPort)} sau ", "s"));
                else
                    shipEtaLabels.Add((s, loc, $"TỚI {ShortPortName(s.Route.ToPort)} · còn ", $"s{cargo}"));
            }
            else card.Add(Line(LocationLine(s)));

            card.RegisterCallback<ClickEvent>(_ => SelectShip(s.Id));
            return card;
        }

        // Tooltip đầy đủ: tên cảng đầy đủ + tính cách — gộp thông tin bị rút gọn trong thẻ.
        string ShipCardTooltip(ShipState s, ShipDef def)
        {
            var parts = new List<string> { s.DisplayName, $"Thuyền trưởng {CaptainName(s.Captain)}" };
            if (def != null && !string.IsNullOrEmpty(def.traits)) parts.Add(def.traits);
            if (s.Status == ShipStatus.Idle) parts.Add($"Đang đậu ở {PortName(s.PortId)}");
            else if (s.Route != null)
                parts.Add($"Đang đi {PortName(s.Route.portPath[0])} → {PortName(s.Route.portPath[s.Route.portPath.Count - 1])}");
            if (!string.IsNullOrEmpty(s.OrderId))
            {
                var o = state.Orders.Find(x => x.Id == s.OrderId);
                if (o != null) parts.Add($"Chở: {o.CustomerName} → {PortName(o.ToPortId)}");
            }
            return string.Join("\n", parts);
        }

        // Dòng vị trí khi ĐẬU — nhãn cảng ngắn, đơn đang chở gộp vào dòng. Tàu đang đi dùng
        // nhãn ETA tại chỗ (shipEtaLabels) nên không rơi vào đây.
        string LocationLine(ShipState s)
        {
            string cargo = string.IsNullOrEmpty(s.OrderId) ? "" : $" · CHỞ {s.OrderId}";
            return $"ĐẬU: {ShortPortName(s.PortId)}{cargo}";
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

        // ---- vùng C: hàng đơn chờ điều tàu ------------------------------------

        VisualElement BuildOrdersRow(float savedOffset)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("fleet-orders");
            var list = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId));
            wrap.Add(Section($"ĐƠN CHỜ ĐIỀU TÀU ({list.Count})"));
            // Thẻ 1 dòng, khung tối đa ~2 thẻ — nhiều hơn thì CUỘN, không tràn ra vùng khác.
            var scroll = CRTScroll();
            scroll.AddToClassList("fleet-orders-scroll");
            var row = new VisualElement();
            row.AddToClassList("fleet-orders-row");
            if (list.Count == 0) row.Add(Line("Không có đơn chờ — nhận đơn ở tab ĐƠN HÀNG."));
            var ship = state.Ships.Find(s => s.Id == selectedShipId);
            foreach (var o in list) row.Add(OrderCard(o, ship));
            scroll.Add(row);
            wrap.Add(scroll);
            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, savedOffset)).ExecuteLater(1);
            return wrap;
        }

        VisualElement OrderCard(OrderState o, ShipState ship)
        {
            var card = new VisualElement();
            card.AddToClassList("fleet-order-card");
            if (o.Id == selectedOrderId) card.AddToClassList("selected");
            // Tàu đang chọn → tô đơn khớp cảng đi, mờ đơn không giao được cho tàu này.
            if (ship != null)
                card.AddToClassList(ship.Status == ShipStatus.Idle && ship.PortId == o.FromPortId ? "compatible" : "dimmed");
            var tags = string.Join(" · ", CargoTags(o.Tags).Select(t => t.name));
            card.tooltip = $"{o.Id} — {o.CustomerName}\n{PortName(o.FromPortId)} → {PortName(o.ToPortId)}"
                + (tags.Length > 0 ? $"\n{tags}" : "");

            // G6.3: 2 dòng — khách → đích (nhãn ngắn), hạn 10 Hz + doanh thu + chip ký hiệu sẵn có.
            card.Add(Title($"{o.CustomerName} → {ShortPortName(o.ToPortId)}"));
            var meta = new VisualElement();
            meta.AddToClassList("fleet-order-meta");
            meta.Add(CountdownLabel(o, "hạn ", $" · +{o.Revenue}"));
            foreach (var (tag, name) in CargoTags(o.Tags))
                if (tag != OrderTag.None) meta.Add(Chip(CargoChipClass(tag), SymbolOf(name)));
            card.Add(meta);

            card.RegisterCallback<ClickEvent>(_ => SelectOrder(o.Id));
            return card;
        }

        // "[!] Dễ vỡ" → "[!]" — thẻ gọn chỉ vừa ký hiệu; tên đầy đủ đã ở tooltip của thẻ.
        static string SymbolOf(string tagText)
        {
            int close = tagText.IndexOf(']');
            return close >= 0 ? tagText.Substring(0, close + 1) : tagText;
        }

        const string RoamWaitPrefix = "Tàu đang đi, chỉ ra lệnh được khi cập cảng (tới sau ";

        // ---- panel lệnh tối giản (G6.3) -----------------------------------------
        // 1 dòng hướng dẫn theo trạng thái + tối đa 2 nút tuyến TO (bấm = xuất phát ngay).
        // Điều rỗng là nút phụ nhỏ cuối panel. Không còn hàng TÀU/ĐƠN hay nút chuyển chế độ lớn.

        // Nhãn ETA tại chỗ cho tàu đang đi trong panel lệnh (10 Hz — shipEtaLabels).
        void AddShipWaitLabel(VisualElement parent, ShipState ship)
        {
            var wait = new Label("");
            wait.AddToClassList("order-line");
            parent.Add(wait);
            if (ship.Route == null) { wait.text = "Tàu không có tuyến."; return; }
            if (ship.Status == ShipStatus.Roaming)
                shipEtaLabels.Add((ship, wait, RoamWaitPrefix, "s)"));
            else
                shipEtaLabels.Add((ship, wait, $"Đang đi tới {ShortPortName(ship.Route.ToPort)}, còn ", "s"));
        }

        // Nhãn nút theo length: ngắn nhất = NHANH, còn lại = AN TOÀN; bằng nhau → nhãn gốc RouteOption.
        static string RouteChoiceLabel(List<RouteOption> options, int idx)
        {
            float min = options.Min(o => o.length);
            if (options.All(o => Mathf.Approximately(o.length, min))) return options[idx].label;
            return Mathf.Approximately(options[idx].length, min) ? "NHANH" : "AN TOÀN";
        }

        // Nguy hiểm của tuyến, contextual thay chú giải (G6.1): chỉ khi có cạnh không Clear — "! Sương mù".
        static string HazardText(RouteOption opt)
        {
            var hs = opt.hazards
                .Where(h => h != HazardType.Clear && h != HazardType.Any)
                .Distinct().ToList();
            return hs.Count == 0 ? "" : " · ! " + string.Join(" · ! ", hs.Select(HazardShort));
        }

        static string HazardShort(HazardType h) => h switch
        {
            HazardType.Fog => "Sương mù", HazardType.Storm => "Bão",
            HazardType.Pirates => "Cướp", HazardType.Ice => "Băng", _ => h.ToString(),
        };

        // Nút tuyến = CHỌN + GIAO trong MỘT cú bấm (FleetActions.Assign áp NGAY như cũ).
        Button RouteButton(ShipState ship, OrderState order, List<RouteOption> options, int idx)
        {
            var opt = options[idx];
            var btn = new Button(() =>
            {
                var err = FleetActions.Assign(state, routeTable, ship.Id, order.Id, idx);
                if (err == null)
                {
                    fleetFeedback = $"Đã giao {order.Id} cho {ship.DisplayName} — khởi hành ngay.";
                    selectedOrderId = null; // đơn vào trạng thái Đang chở → bỏ chọn cả hai
                    selectedShipId = null;
                    fleetError = null;
                }
                else fleetError = err;
                Dirty();
            }) { text = $"{RouteChoiceLabel(options, idx)} · {EtaSeconds(opt, ship):0}s{HazardText(opt)}" };
            btn.AddToClassList("btn-route");
            btn.AddToClassList("btn-big");
            btn.tooltip = $"{PortName(order.FromPortId)} → {PortName(order.ToPortId)}";
            return btn;
        }

        VisualElement BuildCommandBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("fleet-cmd");

            var ship = state.Ships.Find(s => s.Id == selectedShipId);
            var order = SelectedOrder();

            // Phản hồi / lỗi lệnh — mỗi thứ tối đa 1 dòng, luôn ở đầu panel.
            var note = new VisualElement();
            note.AddToClassList("fleet-cmd-note");
            if (!string.IsNullOrEmpty(fleetFeedback))
            {
                var fb = Line(fleetFeedback);
                fb.AddToClassList("fleet-feedback");
                note.Add(fb);
            }
            if (!string.IsNullOrEmpty(fleetError)) note.Add(Warn(fleetError));

            // Dòng hướng dẫn DUY NHẤT, đổi theo trạng thái — thay mọi dòng TÀU/ĐƠN/mô tả dài.
            var guide = new Label("");
            guide.AddToClassList("order-line");

            if (fleetMode == "reposition")
            {
                guide.text = ship != null
                    ? $"ĐIỀU RỖNG {ship.DisplayName} — chọn cảng đích trên bản đồ, hoặc bấm tuyến:"
                    : "Chọn một tàu để điều rỗng.";
                bar.Add(guide);
                var actions = new VisualElement();
                actions.AddToClassList("fleet-cmd-actions");
                BuildRepositionActions(note, actions, ship);
                note.Add(actions);
                bar.Add(note);
                var back = new Button(() => { fleetMode = "assign"; fleetError = null; fleetFeedback = null; Dirty(); })
                    { text = "← GIAO ĐƠN" };
                back.AddToClassList("btn-sub");
                bar.Add(back);
                return bar;
            }

            if (order == null)
            {
                guide.text = "Bấm một đơn để giao.";
                bar.Add(guide);
            }
            else if (ship == null)
            {
                guide.text = $"Chọn tàu đang ở {ShortPortName(order.FromPortId)}.";
                bar.Add(guide);
            }
            else if (ship.Status != ShipStatus.Idle)
            {
                // Tàu đang chạy/đang đi rảnh: không nút — 1 dòng ETA cập 10 Hz (Việc 1).
                guide.text = "Tàu đang đi — chờ cập cảng để giao.";
                bar.Add(guide);
                AddShipWaitLabel(bar, ship);
            }
            else
            {
                var options = routeTable.GetOptions(order.FromPortId, order.ToPortId);
                if (options == null || options.Count == 0)
                {
                    guide.text = "Không có tuyến đường cho cặp cảng này.";
                    bar.Add(guide);
                }
                else
                {
                    guide.text = "Chọn đường đi.";
                    bar.Add(guide);
                    // Tối đa 2 nút, ngắn nhất trước — bấm là khởi hành, không cần nút GIAO LỆNH riêng.
                    foreach (var (opt, idx) in options.Select((o, i) => (o, i)).OrderBy(x => x.o.length).Take(2))
                        bar.Add(RouteButton(ship, order, options, idx));
                }
            }

            bar.Add(note);

            // Nút phụ nhỏ: điều tàu rỗng — chỉ khi đã chọn tàu rảnh (G6.3).
            if (ship != null && ship.Status == ShipStatus.Idle)
            {
                var sub = new Button(() =>
                {
                    fleetMode = "reposition";
                    selectedReposPortId = null;
                    fleetError = null;
                    fleetFeedback = null;
                    Dirty();
                }) { text = "Chuyển tàu rỗng sang cảng khác" };
                sub.AddToClassList("btn-sub");
                bar.Add(sub);
            }
            return bar;
        }

        void BuildRepositionActions(VisualElement note, VisualElement actions, ShipState ship)
        {
            if (ship == null)
            {
                note.Add(Line("Chọn một tàu để điều rỗng."));
                return;
            }
            if (ship.Status != ShipStatus.Idle)
            {
                // G6.3/Việc 1: tàu đang đi — ETA cập tại chỗ 10 Hz, không phải chữ tĩnh.
                AddShipWaitLabel(note, ship);
                return;
            }

            // Cảng đích đã chọn trên bản đồ → chỉ còn chọn tuyến và khởi hành ngay (lệnh áp NGAY như cũ).
            if (!string.IsNullOrEmpty(selectedReposPortId))
            {
                var options = routeTable.GetOptions(ship.PortId, selectedReposPortId);
                if (options != null)
                {
                    for (int i = 0; i < options.Count; i++)
                    {
                        int idx = i;
                        var opt = options[i];
                        var btn = new Button(() =>
                        {
                            var err = FleetActions.Reposition(state, routeTable, ship.Id, selectedReposPortId, idx);
                            if (err == null)
                            {
                                fleetFeedback = $"Đã điều {ship.DisplayName} rỗng → {PortName(selectedReposPortId)} — khởi hành ngay.";
                                selectedReposPortId = null;
                                fleetError = null;
                            }
                            else fleetError = err;
                            Dirty();
                        }) { text = $"{opt.label} · {opt.length:0.#} · {EtaSeconds(opt, ship):0}s" };
                        btn.AddToClassList("btn-route");
                        actions.Add(btn);
                    }
                }
                var reset = new Button(() => { selectedReposPortId = null; Dirty(); }) { text = "TẤT CẢ CẢNG" };
                reset.AddToClassList("btn-mode");
                actions.Add(reset);
                return;
            }

            note.Add(Line("Chọn cảng đích trên bản đồ, hoặc bấm tuyến:"));
            // Chưa chọn cảng: liệt kê mọi cảng đi được (hành vi cũ — bấm là khởi hành ngay).
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
                    var toPort = kv.Key;
                    var btn = new Button(() =>
                    {
                        var err = FleetActions.Reposition(state, routeTable, ship.Id, toPort, idx);
                        if (err == null)
                        {
                            fleetFeedback = $"Đã điều {ship.DisplayName} rỗng → {kv.Value.displayName} — khởi hành ngay.";
                            fleetError = null;
                        }
                        else fleetError = err;
                        Dirty();
                    }) { text = $"→ {ShortPortName(toPort)} · {EtaSeconds(opt, ship):0}s" };
                    btn.AddToClassList("btn-route");
                    actions.Add(btn);
                    any = true;
                }
            }
            if (!any) actions.Add(Line("Không có tuyến đi rỗng từ cảng này."));
        }
    }
}
