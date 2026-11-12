using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    // FR-0103 (chốt lệnh bất đảo), FR-0304 (hết hạn inbox), FR-0409/FR-0501 (chạy tàu, giao hàng),
    // FR-0506 (Δ tiến độ từ sự cố, không âm).
    public static class SimulationStep
    {
        // MAKE IT SO: thực thi mọi lệnh staged + hết hạn inbox. Không thể quay lại.
        public static void CommitStaged(GameState state)
        {
            foreach (var cmd in state.StagedCommands)
            {
                var ship = state.Ships.Find(s => s.Id == cmd.ShipId);
                if (ship == null || ship.Status != ShipStatus.Idle) continue;
                if (cmd.Route == null) continue;
                ship.Route = cmd.Route;
                ship.Status = ShipStatus.Moving;
                ship.DistanceAlongRoute = 0f;
                ship.ArrivedPortId = "";
                if (cmd.Kind == CommandKind.Assign)
                {
                    var order = state.Orders.Find(o => o.Id == cmd.OrderId);
                    if (order == null) { ship.Status = ShipStatus.Idle; ship.Route = null; continue; }
                    ship.OrderId = order.Id;
                    order.Status = OrderStatus.Assigned;
                    order.AssignedShipId = ship.Id;
                }
            }
            state.StagedCommands.Clear();

            // FR-0304
            for (int i = 0; i < state.Orders.Count; i++)
            {
                if (state.Orders[i].Status == OrderStatus.InInbox)
                    state.Orders[i].Status = OrderStatus.Declined;
            }
        }

        // shortcut: các cạnh của một RouteOption coi như đều nhau (length chia theo số cạnh) — dữ liệu mẫu chỉ cần hazard theo cạnh, không cần độ dài từng cạnh
        private static float Cumulative(RouteOption route, int nodeIndex)
        {
            int edges = route.portPath.Count - 1;
            return edges <= 0 ? 0f : route.length * nodeIndex / edges;
        }

        // FR-0501: mỗi tàu Moving tiến speed route-units; tới đích thì giao (FR-0409).
        public static void MoveAll(GameState state)
        {
            foreach (var ship in state.Ships)
            {
                if (ship.Status != ShipStatus.Moving || ship.Route == null) continue;
                ship.ArrivedPortId = "";
                var route = ship.Route;
                ship.DistanceAlongRoute += state.Config.SpeedValue(ship.Speed);
                // node giữa vừa đi qua (không tính đích cuối)
                for (int i = 1; i < route.portPath.Count - 1; i++)
                {
                    float nodeDist = Cumulative(route, i);
                    if (ship.DistanceAlongRoute >= nodeDist - 0.0001f && nodeDist < route.length - 0.0001f)
                        ship.ArrivedPortId = route.portPath[i];
                }
                if (ship.DistanceAlongRoute >= route.length - 0.0001f)
                    FinalizeArrival(state, ship);
            }
        }

        // FR-0506: Δ tiến độ từ sự cố; không âm; chạm đích thì giao luôn.
        public static void ApplyProgressDelta(GameState state, ShipState ship, float delta)
        {
            if (ship.Status != ShipStatus.Moving || ship.Route == null) return;
            ship.DistanceAlongRoute = Mathf.Max(0f, ship.DistanceAlongRoute + delta);
            if (ship.DistanceAlongRoute >= ship.Route.length - 0.0001f)
                FinalizeArrival(state, ship);
        }

        // FR-0409: tới cảng đến của đơn → Delivered; tàu Idle.
        private static void FinalizeArrival(GameState state, ShipState ship)
        {
            ship.PortId = ship.Route.ToPort;
            ship.Status = ShipStatus.Idle;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
            if (!string.IsNullOrEmpty(ship.OrderId))
            {
                var order = state.Orders.Find(o => o.Id == ship.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.Delivered;
                    order.DeliveredDay = state.CurrentDay;
                    order.AssignedShipId = ship.Id;
                }
                ship.OrderId = "";
            }
            ship.Route = null;
        }

        // FR-0502: ngữ cảnh sự cố cho các tàu còn Moving sau Simulation.
        public static List<IncidentContext> MovingContexts(GameState state)
        {
            var list = new List<IncidentContext>();
            foreach (var ship in state.Ships)
            {
                if (ship.Status != ShipStatus.Moving || ship.Route == null) continue;
                OrderState carried = string.IsNullOrEmpty(ship.OrderId)
                    ? null
                    : state.Orders.Find(o => o.Id == ship.OrderId);
                var hazards = new List<HazardType>(ship.Route.hazards);
                list.Add(new IncidentContext
                {
                    Ship = ship,
                    CarriedOrder = carried,
                    JustArrivedAtPort = !string.IsNullOrEmpty(ship.ArrivedPortId),
                    EdgeHazards = hazards
                });
            }
            return list;
        }
    }
}
