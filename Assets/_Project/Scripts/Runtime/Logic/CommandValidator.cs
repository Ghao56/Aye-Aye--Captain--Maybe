using UnityEngine;
namespace AyeAye
{

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

        public static float RoamRemainSeconds(ShipState ship, GameConfig config)
        {
            float edgeSpeed = config.SpeedUnitsPerSecond(ship.Speed) * Mathf.Max(0.0001f, config.roamSpeedFactor);
            return (ship.Route.length - ship.DistanceAlongRoute) / edgeSpeed;
        }

        public static string ValidateAssign(GameState state, RouteTable routes, string shipId, string orderId, int routeOptionIndex)
        {
            var ship = state.Ships.Find(s => s.Id == shipId);
            if (ship == null) return "Ship not found.";
            if (ship.Status == ShipStatus.Roaming)
                return $"{ship.DisplayName} is sailing to {ship.Route.ToPort} ({Mathf.CeilToInt(RoamRemainSeconds(ship, state.Config))}s left) — commands only when it docks.";
            if (ship.Status != ShipStatus.Idle) return $"{ship.DisplayName} is busy, no new orders.";
            var order = state.Orders.Find(o => o.Id == orderId);
            if (order == null) return "Order not found.";
            if (order.Status != OrderStatus.Accepted) return $"Order {orderId} is not accepted, or already has a ship.";
            if (order.AssignedShipId != null && order.AssignedShipId != shipId) return $"Order {orderId} was already taken by another ship.";
            if (ship.PortId != order.FromPortId) return $"{ship.DisplayName} is not at the order's departure port ({order.FromPortId}).";
            var options = routes.GetOptions(order.FromPortId, order.ToPortId);
            if (options == null || routeOptionIndex < 0 || routeOptionIndex >= options.Count) return "No valid route for this port pair.";
            return null;
        }

        public static string ValidateReposition(GameState state, RouteTable routes, string shipId, string toPortId, int routeOptionIndex)
        {
            var ship = state.Ships.Find(s => s.Id == shipId);
            if (ship == null) return "Ship not found.";
            if (ship.Status == ShipStatus.Roaming)
                return $"{ship.DisplayName} is sailing to {ship.Route.ToPort} ({Mathf.CeilToInt(RoamRemainSeconds(ship, state.Config))}s left) — commands only when it docks.";
            if (ship.Status != ShipStatus.Idle) return $"{ship.DisplayName} is busy, no new orders.";
            if (!string.IsNullOrEmpty(ship.OrderId)) return $"{ship.DisplayName} is carrying cargo — no empty run.";
            var options = routes.GetOptions(ship.PortId, toPortId);
            if (options == null || routeOptionIndex < 0 || routeOptionIndex >= options.Count) return "No valid route for this port pair.";
            return null;
        }
    }
}

