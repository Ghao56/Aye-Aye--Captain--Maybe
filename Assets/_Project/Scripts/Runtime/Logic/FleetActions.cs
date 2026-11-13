using System.Collections.Generic;

namespace AyeAye
{
    // Lệnh đội tàu từ UI: lớp thuần giống OrderActions — UI chỉ render + intent, không chứa luật.
    // MAKE IT SO trên thẻ lệnh áp NGAY (không staged, không hủy được — chỉnh 4 của chủ dự án).
    // Trả về null khi thành công; khác null là lý do từ chối (chuỗi tiếng Việt từ CommandValidator).
    public static class FleetActions
    {
        public static string Assign(GameState state, RouteTable routes, string shipId, string orderId, int routeOptionIndex)
        {
            string error = CommandValidator.ValidateAssign(state, routes, shipId, orderId, routeOptionIndex);
            if (error != null) return error;
            var order = state.Orders.Find(o => o.Id == orderId);
            var ship = state.Ships.Find(s => s.Id == shipId);
            var options = routes.GetOptions(order.FromPortId, order.ToPortId);
            LaunchShip(state, ship, order, options[routeOptionIndex]);
            return null;
        }

        public static string Reposition(GameState state, RouteTable routes, string shipId, string toPortId, int routeOptionIndex)
        {
            string error = CommandValidator.ValidateReposition(state, routes, shipId, toPortId, routeOptionIndex);
            if (error != null) return error;
            var ship = state.Ships.Find(s => s.Id == shipId);
            var options = routes.GetOptions(ship.PortId, toPortId);
            LaunchShip(state, ship, null, options[routeOptionIndex]);
            return null;
        }

        // Áp lệnh NGAY: tàu xuất phát tức thì (không dịch chuyển tức thời, không quay đầu giữa cạnh).
        static void LaunchShip(GameState state, ShipState ship, OrderState order, RouteOption route)
        {
            ship.Route = route;
            ship.Status = ShipStatus.Moving;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
            if (order != null)
            {
                ship.OrderId = order.Id;
                order.Status = OrderStatus.Assigned;
                order.AssignedShipId = ship.Id;
            }
        }
    }
}
