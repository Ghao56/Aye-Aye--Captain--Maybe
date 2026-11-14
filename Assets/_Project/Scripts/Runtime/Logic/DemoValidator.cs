using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AyeAye
{
    /// <summary>
    /// FR-0905 Validate (thuần C#, có test): tham chiếu đủ, bảng outcome đủ (FR-0505),
    /// route nối được, không có đơn không thể giao đúng hạn bởi tàu nhanh nhất. Menu Tools/AyeAye/Validate chỉ gọi lớp này trên assets.
    /// ports/edges tuỳ chọn: cung cấp thì kiểm tra thêm kết nối và id cảng.
    /// </summary>
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

            if (config == null) errors.Add("Thiếu GameConfig");
            if (routes == null) errors.Add("Thiếu RouteTable");
            if (orderPool == null) errors.Add("Thiếu OrderPool");
            if (ships == null || ships.Count == 0) errors.Add("Không có ShipDef nào");
            if (crews == null || crews.Count == 0) errors.Add("Không có CrewDefinition nào");

            if (ports != null)
            {
                var ids = new HashSet<string>();
                foreach (var p in ports)
                {
                    if (p == null) { errors.Add("PortDef null trong danh sách"); continue; }
                    if (string.IsNullOrEmpty(p.portId) || !ids.Add(p.portId))
                        errors.Add($"PortDef id trống hoặc trùng: {p.portId}");
                }
                if (edges != null)
                {
                    foreach (var e in edges)
                    {
                        if (e == null) { errors.Add("RouteDef null trong danh sách"); continue; }
                        if (!ids.Contains(e.fromPortId) || !ids.Contains(e.toPortId))
                            errors.Add($"RouteDef nối tới cảng không tồn tại: {e.fromPortId}–{e.toPortId}");
                        if (e.length <= 0)
                            errors.Add($"RouteDef {e.fromPortId}–{e.toPortId} độ dài ≤ 0");
                    }
                }
            }

            // Đơn: từ/đến hợp lệ và có ít nhất 1 Route Option nối được (không có đơn không thể giao).
            var orders = new List<OrderDef>();
            var fixedIds = new HashSet<string>(); // đơn tutorial — được cộng thêm biên tutorialDeadlineBonusSeconds
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
                if (o == null) { errors.Add("OrderDef null trong pool"); continue; }
                if (string.IsNullOrEmpty(o.orderId)) errors.Add("OrderDef thiếu orderId");
                if (o.deadlineSeconds <= 0f) errors.Add($"{o.orderId}: deadlineSeconds <= 0");
                if (routes != null && routes.GetOptions(o.fromPortId, o.toPortId) == null)
                    errors.Add($"{o.orderId}: không có tuyến nào nối {o.fromPortId} → {o.toPortId} (đơn không thể giao)");
                // Đơn phải giao nổi đúng hạn bởi tàu nhanh nhất + biên phản ứng người chơi (chỉnh 6, mục 2.5).
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
                            errors.Add($"{o.orderId}: hạn {o.deadlineSeconds:0.#}s < ETA nhanh nhất {fastest:0.#}s + biên phản ứng {margin:0.#}s (người chơi không kịp thao tác)");
                    }
                }
            }

            // Route option: các portPath phải nối được bằng cạnh có thật (khi có danh sách cạnh).
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
                            errors.Add($"RouteOption rỗng/hỏng tại {pair.fromPortId}→{pair.toPortId}");
                            continue;
                        }
                        for (int i = 0; i < opt.portPath.Count - 1; i++)
                            if (!connected.Contains(Key(opt.portPath[i], opt.portPath[i + 1])))
                                errors.Add($"{opt.label}: chặng {opt.portPath[i]}–{opt.portPath[i + 1]} không có cạnh thật");
                        if (opt.length <= 0f)
                            errors.Add($"{opt.label}: độ dài ≤ 0");
                    }
                }
            }

            // Tàu: id duy nhất; cảng khởi đầu phải tồn tại (khi có danh sách cảng).
            if (ships != null)
            {
                var shipIds = new HashSet<string>();
                var portIds = ports != null ? new HashSet<string>(ports.Where(p => p != null).Select(p => p.portId)) : null;
                foreach (var s in ships)
                {
                    if (s == null) { errors.Add("ShipDef null trong danh sách"); continue; }
                    if (string.IsNullOrEmpty(s.shipId) || !shipIds.Add(s.shipId))
                        errors.Add($"ShipDef id trống hoặc trùng: {s.shipId}");
                    if (portIds != null && !portIds.Contains(s.startPortId))
                        errors.Add($"{s.shipId}: cảng khởi đầu {s.startPortId} không tồn tại");
                    // G5: hồ sơ roaming — dwell > 0, mọi hệ số hữu hạn.
                    if (s.roam != null)
                    {
                        if (s.roam.dwellSeconds <= 0f)
                            errors.Add($"{s.shipId}: roam dwellSeconds <= 0");
                        if (!IsFinite(s.roam.demandWeight) || !IsFinite(s.roam.farBias)
                            || !IsFinite(s.roam.hubAffinity) || !IsFinite(s.roam.wanderWeight)
                            || !IsFinite(s.roam.backtrackPenalty))
                            errors.Add($"{s.shipId}: hệ số roam không hữu hạn (NaN/∞)");
                    }
                }
            }

            // Sự cố: bảng outcome đủ (FR-0505), kể cả sự cố cố định ngày 1/ngày cuối.
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
