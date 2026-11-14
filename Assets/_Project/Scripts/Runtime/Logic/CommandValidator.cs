using UnityEngine;

namespace AyeAye
{
    // FR-0303, FR-0404, FR-0405: kiểm tra tính hợp lệ của lệnh trước khi áp (MAKE IT SO áp ngay).
    // Trả về null = hợp lệ; khác null = lý do từ chối (tiếng Việt).
    public static class CommandValidator
    {
        private static int ActiveCount(GameState state)
        {
            int count = 0;
            for (int i = 0; i < state.Orders.Count; i++)
            {
                var st = state.Orders[i].Status;
                if (st == OrderStatus.Accepted || st == OrderStatus.Assigned) count++;
            }
            return count;
        }

        public static bool CanAccept(GameState state, string orderId)
        {
            return ActiveCount(state) < state.Config.maxActiveOrders;
        }

        // G5: thời gian còn lại của tàu Roaming tới đích cạnh (giây, làm tròn lên).
        public static float RoamRemainSeconds(ShipState ship, GameConfig config)
        {
            float edgeSpeed = config.SpeedUnitsPerSecond(ship.Speed) * Mathf.Max(0.0001f, config.roamSpeedFactor);
            return (ship.Route.length - ship.DistanceAlongRoute) / edgeSpeed;
        }

        public static string ValidateAssign(GameState state, RouteTable routes, string shipId, string orderId, int routeOptionIndex)
        {
            var ship = state.Ships.Find(s => s.Id == shipId);
            if (ship == null) return "Không tìm thấy tàu.";
            if (ship.Status == ShipStatus.Roaming)
                return $"{ship.DisplayName} đang đi tới {ship.Route.ToPort} (còn {Mathf.CeilToInt(RoamRemainSeconds(ship, state.Config))}s) — chỉ ra lệnh khi cập cảng.";
            if (ship.Status != ShipStatus.Idle) return $"{ship.DisplayName} đang bận, không nhận lệnh mới.";
            var order = state.Orders.Find(o => o.Id == orderId);
            if (order == null) return "Không tìm thấy đơn.";
            if (order.Status != OrderStatus.Accepted) return $"Đơn {orderId} chưa vào 'Đã nhận' hoặc đã có tàu chở.";
            if (order.AssignedShipId != null && order.AssignedShipId != shipId) return $"Đơn {orderId} đã có tàu khác nhận.";
            if (ship.PortId != order.FromPortId) return $"{ship.DisplayName} không ở cảng đi của đơn ({order.FromPortId}).";
            var options = routes.GetOptions(order.FromPortId, order.ToPortId);
            if (options == null || routeOptionIndex < 0 || routeOptionIndex >= options.Count) return "Không có tuyến đường hợp lệ cho cặp cảng này.";
            return null;
        }

        public static string ValidateReposition(GameState state, RouteTable routes, string shipId, string toPortId, int routeOptionIndex)
        {
            var ship = state.Ships.Find(s => s.Id == shipId);
            if (ship == null) return "Không tìm thấy tàu.";
            if (ship.Status == ShipStatus.Roaming)
                return $"{ship.DisplayName} đang đi tới {ship.Route.ToPort} (còn {Mathf.CeilToInt(RoamRemainSeconds(ship, state.Config))}s) — chỉ ra lệnh khi cập cảng.";
            if (ship.Status != ShipStatus.Idle) return $"{ship.DisplayName} đang bận, không nhận lệnh mới.";
            if (!string.IsNullOrEmpty(ship.OrderId)) return $"{ship.DisplayName} đang chở hàng, không điều phóng rỗng được.";
            var options = routes.GetOptions(ship.PortId, toPortId);
            if (options == null || routeOptionIndex < 0 || routeOptionIndex >= options.Count) return "Không có tuyến đường hợp lệ cho cặp cảng này.";
            return null;
        }
    }
}
