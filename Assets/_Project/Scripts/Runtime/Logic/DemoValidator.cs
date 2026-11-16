using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AyeAye
{

    public static class DemoValidator
    {

        public static List<string> Validate(
            GameConfig config,
            RouteTable routes,
            OrderPool orderPool,
            IReadOnlyList<IncidentDef> incidents,
            IncidentDef fixedFirstDay,
            IncidentDef fixedFinale,
            IReadOnlyList<ShipDef> ships,
            IReadOnlyList<CrewDefinition> crews,
            IEnumerable<OrderDef> extraOrders = null,
            IEnumerable<IncidentDef> extraIncidents = null,
            IReadOnlyList<PortDef> ports = null,
            IReadOnlyList<RouteDef> edges = null)
        {
            var errors = new List<string>();
            if (config == null) errors.Add("Missing GameConfig");
            else
            {
                if (config.orderDeadlineExtraMaxSeconds < config.orderDeadlineExtraMinSeconds)
                    errors.Add("GameConfig: orderDeadlineExtraMaxSeconds < orderDeadlineExtraMinSeconds");
                if (config.orderRevenueBase <= 0) errors.Add("GameConfig: orderRevenueBase <= 0");
                if (config.roamStartSeconds < config.orderFirstDelaySeconds)
                    errors.Add($"GameConfig: roamStartSeconds {config.roamStartSeconds:0.#} < orderFirstDelaySeconds {config.orderFirstDelaySeconds:0.#} (ships must wait for the first order before leaving port)");
            }
            if (routes == null) errors.Add("Missing RouteTable");
            if (orderPool == null) errors.Add("Missing OrderPool");
            if (ships == null || ships.Count == 0) errors.Add("No ShipDef found");
            if (crews == null || crews.Count == 0) errors.Add("No CrewDefinition found");
            if (ports != null)
            {
                var ids = new HashSet<string>();
                foreach (var p in ports)
                {
                    if (p == null) { errors.Add("Null PortDef in the list"); continue; }
                    if (string.IsNullOrEmpty(p.portId) || !ids.Add(p.portId))
                        errors.Add($"PortDef id empty or duplicate: {p.portId}");
                }
                if (edges != null)
                {
                    foreach (var e in edges)
                    {
                        if (e == null) { errors.Add("Null RouteDef in the list"); continue; }
                        if (!ids.Contains(e.fromPortId) || !ids.Contains(e.toPortId))
                            errors.Add($"RouteDef connects to a non-existent port: {e.fromPortId}–{e.toPortId}");
                        if (e.length <= 0)
                            errors.Add($"RouteDef {e.fromPortId}–{e.toPortId} length ≤ 0");
                    }
                }
            }
            var orders = new List<OrderDef>();
            var fixedIds = new HashSet<string>(); 
            if (orderPool != null)
            {
                if (orderPool.fixedFirstDay != null)
                {
                    orders.AddRange(orderPool.fixedFirstDay);
                    foreach (var o in orderPool.fixedFirstDay) if (o != null) fixedIds.Add(o.orderId);
                }
                if (orderPool.pool != null) orders.AddRange(orderPool.pool);
            }
            if (extraOrders != null) orders.AddRange(extraOrders);
            foreach (var o in orders)
            {
                if (o == null) { errors.Add("Null OrderDef in the pool"); continue; }
                if (string.IsNullOrEmpty(o.orderId)) errors.Add("OrderDef missing orderId");
                if (o.deadlineSeconds <= 0f) errors.Add($"{o.orderId}: deadlineSeconds <= 0");
                if (routes != null && routes.GetOptions(o.fromPortId, o.toPortId) == null)
                    errors.Add($"{o.orderId}: no route connects {o.fromPortId} → {o.toPortId} (order can't be delivered)");
                if (config != null && routes != null && o.deadlineSeconds > 0f)
                {
                    var opts = routes.GetOptions(o.fromPortId, o.toPortId);
                    if (opts != null)
                    {
                        float minLen = float.MaxValue;
                        foreach (var opt in opts)
                            if (opt != null && opt.length < minLen) minLen = opt.length;
                        float fastest = minLen / Mathf.Max(0.0001f, config.SpeedUnitsPerSecond(SpeedClass.Fast));
                        float margin = Mathf.Max(0f, config.deadlineReactionMarginSeconds)
                            + (fixedIds.Contains(o.orderId) ? Mathf.Max(0f, config.tutorialDeadlineBonusSeconds) : 0f);
                        if (fastest + margin > o.deadlineSeconds)
                            errors.Add($"{o.orderId}: deadline {o.deadlineSeconds:0.#}s < fastest ETA {fastest:0.#}s + reaction margin {margin:0.#}s (player can't act in time)");
                    }
                }
            }
            if (routes != null && routes.pairs != null)
            {
                var portIds = new List<string>();
                foreach (var pair in routes.pairs)
                {
                    if (pair == null) continue;
                    if (!portIds.Contains(pair.fromPortId)) portIds.Add(pair.fromPortId);
                    if (!portIds.Contains(pair.toPortId)) portIds.Add(pair.toPortId);
                }
                for (int i = 0; i < portIds.Count; i++)
                    for (int j = i + 1; j < portIds.Count; j++)
                        if (routes.GetOptions(portIds[i], portIds[j]) == null)
                            errors.Add($"RouteTable: missing route {portIds[i]} → {portIds[j]} (the generator may spawn orders for this pair)");
            }
            if (edges != null && routes != null)
            {
                var connected = new HashSet<string>();
                foreach (var e in edges)
                {
                    if (e == null) continue;
                    connected.Add(Key(e.fromPortId, e.toPortId));
                    connected.Add(Key(e.toPortId, e.fromPortId));
                }
                foreach (var pair in routes.pairs)
                {
                    if (pair == null || pair.options == null) continue;
                    foreach (var opt in pair.options)
                    {
                        if (opt == null || opt.portPath == null || opt.portPath.Count < 2)
                        {
                            errors.Add($"Empty/broken RouteOption at {pair.fromPortId}→{pair.toPortId}");
                            continue;
                        }
                        for (int i = 0; i < opt.portPath.Count - 1; i++)
                            if (!connected.Contains(Key(opt.portPath[i], opt.portPath[i + 1])))
                                errors.Add($"{opt.label}: leg {opt.portPath[i]}–{opt.portPath[i + 1]} has no real edge");
                        if (opt.length <= 0f)
                            errors.Add($"{opt.label}: length ≤ 0");
                    }
                }
            }
            if (ships != null)
            {
                var shipIds = new HashSet<string>();
                var portIds = ports != null ? new HashSet<string>(ports.Where(p => p != null).Select(p => p.portId)) : null;
                foreach (var s in ships)
                {
                    if (s == null) { errors.Add("Null ShipDef in the list"); continue; }
                    if (string.IsNullOrEmpty(s.shipId) || !shipIds.Add(s.shipId))
                        errors.Add($"ShipDef id empty or duplicate: {s.shipId}");
                    if (portIds != null && !portIds.Contains(s.startPortId))
                        errors.Add($"{s.shipId}: start port {s.startPortId} does not exist");
                    if (s.roam != null)
                    {
                        if (s.roam.dwellSeconds <= 0f)
                            errors.Add($"{s.shipId}: roam dwellSeconds <= 0");
                        if (!IsFinite(s.roam.demandWeight) || !IsFinite(s.roam.farBias)
                            || !IsFinite(s.roam.hubAffinity) || !IsFinite(s.roam.wanderWeight)
                            || !IsFinite(s.roam.backtrackPenalty))
                            errors.Add($"{s.shipId}: roam coefficients not finite (NaN/∞)");
                    }
                }
            }
            var allIncidents = new List<IncidentDef>();
            if (incidents != null) allIncidents.AddRange(incidents);
            if (fixedFirstDay != null) allIncidents.Add(fixedFirstDay);
            if (fixedFinale != null) allIncidents.Add(fixedFinale);
            if (extraIncidents != null) allIncidents.AddRange(extraIncidents);
            foreach (var def in allIncidents)
                if (def != null)
                    errors.AddRange(IncidentResolver.ValidateDef(def, crews));
            return errors;
        }
        static string Key(string a, string b) => a + ">" + b;
        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}

