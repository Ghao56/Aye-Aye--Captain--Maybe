using System.Collections.Generic;

namespace AyeAye
{
    // Lệnh đội tàu từ UI (G3): lớp thuần giống OrderActions — UI chỉ render + intent, không chứa luật.
    // Trả về null khi thành công; khác null là lý do từ chối (chuỗi tiếng Việt từ CommandValidator).
    public static class FleetActions
    {
        public static string Assign(GameState state, RouteTable routes, string shipId, string orderId, int routeOptionIndex)
        {
            string error = CommandValidator.ValidateAssign(state, routes, shipId, orderId, routeOptionIndex);
            if (error != null) return error;
            var order = state.Orders.Find(o => o.Id == orderId);
            var options = routes.GetOptions(order.FromPortId, order.ToPortId);
            state.StagedCommands.Add(new StagedCommand
            {
                Kind = CommandKind.Assign,
                ShipId = shipId,
                OrderId = orderId,
                RouteOptionIndex = routeOptionIndex,
                Route = options[routeOptionIndex],
            });
            return null;
        }

        public static string Reposition(GameState state, RouteTable routes, string shipId, string toPortId, int routeOptionIndex)
        {
            string error = CommandValidator.ValidateReposition(state, routes, shipId, toPortId, routeOptionIndex);
            if (error != null) return error;
            var ship = state.Ships.Find(s => s.Id == shipId);
            var options = routes.GetOptions(ship.PortId, toPortId);
            state.StagedCommands.Add(new StagedCommand
            {
                Kind = CommandKind.Reposition,
                ShipId = shipId,
                OrderId = null,
                RouteOptionIndex = routeOptionIndex,
                Route = options[routeOptionIndex],
            });
            return null;
        }

        // FR-0104: hủy lệnh staged của tàu (chỉ trong pha Lập kế hoạch, trước MAKE IT SO).
        public static string Cancel(GameState state, string shipId)
        {
            var staged = state.StagedCommands.Find(c => c.ShipId == shipId);
            if (staged == null) return "Tàu không có lệnh nào để hủy.";
            state.StagedCommands.Remove(staged);
            return null;
        }
    }
}
