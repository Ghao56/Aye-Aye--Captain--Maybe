using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
namespace AyeAye
{

    public partial class TerminalHud
    {
        Dictionary<string, VisualElement> shipDots;
        VisualElement fleetMap;
        string selectedShipId;
        string selectedOrderId;
        string selectedReposPortId;
        string fleetError;
        string fleetFeedback;
        string fleetMode = "assign";
        static readonly Vector2[] IdleOffsets =
        {
            new Vector2(0f, 0f), new Vector2(0.05f, 0.04f),
            new Vector2(-0.05f, 0.04f), new Vector2(0.05f, -0.04f),
        };
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
            fleetMode = "assign";
            selectedReposPortId = null;
            fleetError = null;
            fleetFeedback = null;
            var o = SelectedOrder();
            if (o != null)
            {
                var fits = state.Ships.FindAll(s => s.Status == ShipStatus.Idle && s.PortId == o.FromPortId);
                if (fits.Count == 1) selectedShipId = fits[0].Id;
            }
            Dirty();
        }
        string ShortPortName(string portId)
        {
            var full = PortName(portId);
            int sp = full.IndexOf(' ');
            return sp > 0 && sp < full.Length - 1 ? full.Substring(sp + 1).ToUpperInvariant() : full.ToUpperInvariant();
        }
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
        void BuildFleetPanel()
        {
            var panel = panels[1];
            float savedSide = SavedScroll(panel, "fleet-side-scroll");
            float savedOrders = SavedScroll(panel, "fleet-orders-scroll");
            panel.Clear();
            shipDots = new Dictionary<string, VisualElement>();
            shipEtaLabels.Clear();
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
                label.pickingMode = PickingMode.Ignore;
                node.Add(label);
                node.tooltip = p.displayName;
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
                var letter = new Label(CaptainName(s.Captain)[0].ToString());
                letter.AddToClassList("map-dot-letter");
                letter.pickingMode = PickingMode.Ignore;
                dot.Add(letter);
                dot.tooltip = $"{s.DisplayName} — captain {s.Captain}";
                dot.RegisterCallback<ClickEvent>(_ => SelectShip(s.Id));
                PlaceDot(dot, s, index);
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
            left.Add(BuildOrdersRow(savedOrders));
            sideScroll.Add(Section("FLEET"));
            sideScroll.Add(grid);
            sideScroll.Add(BuildCommandBar());
            sideScroll.schedule.Execute(() => sideScroll.scrollOffset = new Vector2(0, savedSide)).ExecuteLater(1);
        }
        static float SavedScroll(VisualElement panel, string className)
            => panel.Q(className: className) is ScrollView s ? s.scrollOffset.y : 0f;
        void PlaceDot(VisualElement dot, ShipState ship, int shipIndex)
        {
            Vector2 pos = ship.Status != ShipStatus.Idle && ship.Route != null
                ? AlongRoute(ship.Route, ship.DistanceAlongRoute)
                : ShipAnchor(shipIndex, ship.PortId);
            SetDotPercent(dot, pos);
            dot.EnableInClassList("roaming", ship.Status == ShipStatus.Roaming);
            if (ship.Id == selectedShipId) dot.AddToClassList("selected");
        }
        static void SetDotPercent(VisualElement dot, Vector2 pos)
        {
            dot.style.left = new Length(pos.x * 100f, LengthUnit.Percent);
            dot.style.top = new Length(pos.y * 100f, LengthUnit.Percent);
        }
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
            var n = new Vector2(-dir.y, dir.x);
            switch (h)
            {
                case HazardType.Fog:
                    Dashes(p, a, dir, len, start: 0f, dash: 2f, gap: 6f, width: 4f);
                    break;
                case HazardType.Storm:
                    p.strokeColor = EdgeColor;
                    p.lineWidth = 2f;
                    p.BeginPath();
                    p.MoveTo(a);
                    for (float t = 12f; t < len; t += 12f)
                        p.LineTo(a + dir * t + n * (((int)(t / 12f) & 1) == 1 ? 4f : -4f));
                    p.LineTo(b);
                    p.ClosePath();
                    break;
                case HazardType.Pirates:
                    Dashes(p, a, dir, len, start: 0f, dash: 10f, gap: 6f, width: 3f);
                    break;
                case HazardType.Ice:
                    Dashes(p, a, dir, len, start: 0f, dash: 8f, gap: 4f, width: 2f);
                    Dashes(p, a, dir, len, start: 9f, dash: 2f, gap: 10f, width: 4f);
                    break;
                default:
                    p.strokeColor = EdgeColor;
                    p.lineWidth = 2f;
                    p.BeginPath();
                    p.MoveTo(a);
                    p.LineTo(b);
                    p.ClosePath();
                    break;
            }
        }
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
        static string StatusName(ShipStatus s) => s switch
        {
            ShipStatus.Idle => "IDLE",
            ShipStatus.Roaming => "ROAMING",
            _ => "SAILING",
        };
        VisualElement ShipCard(ShipState s, int shipIndex)
        {
            var card = new VisualElement();
            card.AddToClassList("ship-card");
            if (s.Id == selectedShipId) card.AddToClassList("selected");
            var selOrder = SelectedOrder();
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
                var loc = new Label("");
                loc.AddToClassList("order-line");
                card.Add(loc);
                string cargo = string.IsNullOrEmpty(s.OrderId) ? "" : $" · CARGO {s.OrderId}";
                if (s.Status == ShipStatus.Roaming)
                    shipEtaLabels.Add((s, loc, $"ROAMING · Arrives at {ShortPortName(s.Route.ToPort)} in ", "s"));
                else
                    shipEtaLabels.Add((s, loc, $"TO {ShortPortName(s.Route.ToPort)} · ", $"s left{cargo}"));
            }
            else card.Add(Line(LocationLine(s)));
            card.RegisterCallback<ClickEvent>(_ => SelectShip(s.Id));
            return card;
        }
        string ShipCardTooltip(ShipState s, ShipDef def)
        {
            var parts = new List<string> { s.DisplayName, $"Captain {CaptainName(s.Captain)}" };
            if (def != null && !string.IsNullOrEmpty(def.traits)) parts.Add(def.traits);
            if (s.Status == ShipStatus.Idle) parts.Add($"Docked at {PortName(s.PortId)}");
            else if (s.Route != null)
                parts.Add($"Sailing {PortName(s.Route.portPath[0])} → {PortName(s.Route.portPath[s.Route.portPath.Count - 1])}");
            if (!string.IsNullOrEmpty(s.OrderId))
            {
                var o = state.Orders.Find(x => x.Id == s.OrderId);
                if (o != null) parts.Add($"Cargo: {o.CustomerName} → {PortName(o.ToPortId)}");
            }
            return string.Join("\n", parts);
        }
        string LocationLine(ShipState s)
        {
            string cargo = string.IsNullOrEmpty(s.OrderId) ? "" : $" · CARGO {s.OrderId}";
            return $"DOCKED: {ShortPortName(s.PortId)}{cargo}";
        }
        float EtaSeconds(RouteOption route, ShipState ship)
        {
            if (route == null || ship == null || route.length <= 0.0001f) return 0f;
            return route.length / config.SpeedUnitsPerSecond(ship.Speed);
        }
        static string CaptainName(CrewId c) => c switch
        {
            CrewId.Bob => "Bob", CrewId.Mira => "Mira", CrewId.Chen => "Chen", CrewId.Dex => "Dex",
            _ => c.ToString(),
        };
        static string SpeedName(SpeedClass s) => s switch
        {
            SpeedClass.Fast => "Fast", SpeedClass.Slow => "Slow", _ => "Normal",
        };
        static string ConditionName(ShipCondition c) => c == ShipCondition.Good ? "Good" : "Worn";
        VisualElement BuildOrdersRow(float savedOffset)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("fleet-orders");
            var list = state.Orders.FindAll(o => o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId));
            wrap.Add(Section($"ORDERS AWAITING SHIPS ({list.Count})"));
            var scroll = CRTScroll();
            scroll.AddToClassList("fleet-orders-scroll");
            var row = new VisualElement();
            row.AddToClassList("fleet-orders-row");
            if (list.Count == 0) row.Add(Line("No orders waiting — accept orders in the ORDERS tab."));
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
            if (ship != null)
                card.AddToClassList(ship.Status == ShipStatus.Idle && ship.PortId == o.FromPortId ? "compatible" : "dimmed");
            var tags = string.Join(" · ", CargoTags(o.Tags).Select(t => t.name));
            card.tooltip = $"{o.Id} — {o.CustomerName}\n{PortName(o.FromPortId)} → {PortName(o.ToPortId)}"
                + (tags.Length > 0 ? $"\n{tags}" : "");
            card.Add(Title($"{o.CustomerName} → {ShortPortName(o.ToPortId)}"));
            var meta = new VisualElement();
            meta.AddToClassList("fleet-order-meta");
            meta.Add(CountdownLabel(o, "due ", $" · +{o.Revenue}"));
            foreach (var (tag, name) in CargoTags(o.Tags))
                if (tag != OrderTag.None) meta.Add(Chip(CargoChipClass(tag), SymbolOf(name)));
            card.Add(meta);
            card.RegisterCallback<ClickEvent>(_ => SelectOrder(o.Id));
            return card;
        }
        static string SymbolOf(string tagText)
        {
            int close = tagText.IndexOf(']');
            return close >= 0 ? tagText.Substring(0, close + 1) : tagText;
        }
        const string RoamWaitPrefix = "Ship is away — commands only when it docks (arrives in ";
        void AddShipWaitLabel(VisualElement parent, ShipState ship)
        {
            var wait = new Label("");
            wait.AddToClassList("order-line");
            parent.Add(wait);
            if (ship.Route == null) { wait.text = "Ship has no route."; return; }
            if (ship.Status == ShipStatus.Roaming)
                shipEtaLabels.Add((ship, wait, RoamWaitPrefix, "s)"));
            else
                shipEtaLabels.Add((ship, wait, $"Sailing to {ShortPortName(ship.Route.ToPort)}, ", "s left"));
        }
        static string RouteChoiceLabel(List<RouteOption> options, int idx)
        {
            float min = options.Min(o => o.length);
            if (options.All(o => Mathf.Approximately(o.length, min))) return options[idx].label;
            return Mathf.Approximately(options[idx].length, min) ? "FAST" : "SAFE";
        }
        static string HazardText(RouteOption opt)
        {
            var hs = opt.hazards
                .Where(h => h != HazardType.Clear && h != HazardType.Any)
                .Distinct().ToList();
            return hs.Count == 0 ? "" : " · ! " + string.Join(" · ! ", hs.Select(HazardShort));
        }
        static string HazardShort(HazardType h) => h switch
        {
            HazardType.Fog => "Fog", HazardType.Storm => "Storm",
            HazardType.Pirates => "Pirates", HazardType.Ice => "Ice", _ => h.ToString(),
        };
        Button RouteButton(ShipState ship, OrderState order, List<RouteOption> options, int idx)
        {
            var opt = options[idx];
            var btn = new Button(() =>
            {
                var err = FleetActions.Assign(state, routeTable, ship.Id, order.Id, idx);
                if (err == null)
                {
                    fleetFeedback = $"Assigned {order.Id} to {ship.DisplayName} — departing now.";
                    selectedOrderId = null;
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
            var note = new VisualElement();
            note.AddToClassList("fleet-cmd-note");
            if (!string.IsNullOrEmpty(fleetFeedback))
            {
                var fb = Line(fleetFeedback);
                fb.AddToClassList("fleet-feedback");
                note.Add(fb);
            }
            if (!string.IsNullOrEmpty(fleetError)) note.Add(Warn(fleetError));
            var guide = new Label("");
            guide.AddToClassList("order-line");
            if (fleetMode == "reposition")
            {
                guide.text = ship != null
                    ? $"EMPTY RUN {ship.DisplayName} — pick a destination port on the map, or click a route:"
                    : "Select a ship for an empty run.";
                bar.Add(guide);
                var actions = new VisualElement();
                actions.AddToClassList("fleet-cmd-actions");
                BuildRepositionActions(note, actions, ship);
                note.Add(actions);
                bar.Add(note);
                var back = new Button(() => { fleetMode = "assign"; fleetError = null; fleetFeedback = null; Dirty(); })
                    { text = "← BACK TO ORDERS" };
                back.AddToClassList("btn-sub");
                bar.Add(back);
                return bar;
            }
            if (order == null)
            {
                guide.text = "Click an order to assign it.";
                bar.Add(guide);
            }
            else if (ship == null)
            {
                guide.text = $"Select a ship docked at {ShortPortName(order.FromPortId)}.";
                bar.Add(guide);
            }
            else if (ship.Status != ShipStatus.Idle)
            {
                guide.text = "Ship is away — wait for it to dock to assign.";
                bar.Add(guide);
                AddShipWaitLabel(bar, ship);
            }
            else
            {
                var options = routeTable.GetOptions(order.FromPortId, order.ToPortId);
                if (options == null || options.Count == 0)
                {
                    guide.text = "No route for this port pair.";
                    bar.Add(guide);
                }
                else
                {
                    guide.text = "Choose a route.";
                    bar.Add(guide);
                    foreach (var (opt, idx) in options.Select((o, i) => (o, i)).OrderBy(x => x.o.length).Take(2))
                        bar.Add(RouteButton(ship, order, options, idx));
                }
            }
            bar.Add(note);
            if (ship != null && ship.Status == ShipStatus.Idle)
            {
                var sub = new Button(() =>
                {
                    fleetMode = "reposition";
                    selectedReposPortId = null;
                    fleetError = null;
                    fleetFeedback = null;
                    Dirty();
                }) { text = "Send empty ship to another port" };
                sub.AddToClassList("btn-sub");
                bar.Add(sub);
            }
            return bar;
        }
        void BuildRepositionActions(VisualElement note, VisualElement actions, ShipState ship)
        {
            if (ship == null)
            {
                note.Add(Line("Select a ship for an empty run."));
                return;
            }
            if (ship.Status != ShipStatus.Idle)
            {
                AddShipWaitLabel(note, ship);
                return;
            }
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
                                fleetFeedback = $"Sent {ship.DisplayName} on an empty run → {PortName(selectedReposPortId)} — departing now.";
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
                var reset = new Button(() => { selectedReposPortId = null; Dirty(); }) { text = "ALL PORTS" };
                reset.AddToClassList("btn-mode");
                actions.Add(reset);
                return;
            }
            note.Add(Line("Pick a destination port on the map, or click a route:"));
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
                            fleetFeedback = $"Sent {ship.DisplayName} on an empty run → {kv.Value.displayName} — departing now.";
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
            if (!any) actions.Add(Line("No empty-run routes from this port."));
        }
    }
}

