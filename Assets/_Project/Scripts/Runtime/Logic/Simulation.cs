using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{

    public sealed class SimWorld
    {

        public OrderPool Pool;

        public RouteTable Routes;

        public IReadOnlyList<IncidentDef> Incidents;

        public IncidentDef FixedFirstDay;

        public IncidentDef FixedFinale;

        public string HQPortId = ""; 
    }

    public static class Simulation
    {

        public static void Advance(SimWorld world, GameState state, float dt)
        {
            if (world == null || state == null || dt <= 0f) return;
            if (!state.CanAdvance) return; 
            state.ElapsedSeconds += dt;
            OrderScheduler.SpawnOrders(world, state);
            ExpireInbox(state);   
            MarkLate(state);      
            if (state.IsFinished) return; 
            foreach (var ship in state.Ships)
                AdvanceShip(world, state, ship, dt);
        }

        public static int CalculateTicks(float dt, float simTickSeconds)
        {
            if (simTickSeconds <= 0f) return 1;
            int ticks = Mathf.Max(1, Mathf.FloorToInt(dt / simTickSeconds));
            return ticks;
        }

        public static void AdvanceWithFixedTimestep(SimWorld world, GameState state, float dt)
        {
            if (world == null || state == null || dt <= 0f) return;
            int ticks = CalculateTicks(dt, state.Config.simTickSeconds);
            for (int i = 0; i < ticks && state.CanAdvance; i++)
                Advance(world, state, state.Config.simTickSeconds);
        }

        public static void ApplyProgressDelta(GameState state, ShipState ship, float delta)
        {
            if (ship.Status != ShipStatus.Moving || ship.Route == null) return;
            ship.DistanceAlongRoute = Mathf.Max(0f, ship.DistanceAlongRoute + delta);
            if (ship.DistanceAlongRoute >= ship.Route.length - 0.0001f)
                FinalizeArrival(state, ship);
        }
        static void AdvanceShip(SimWorld world, GameState state, ShipState ship, float dt)
        {
            if (ship.Status == ShipStatus.Roaming)
            {
                AdvanceRoaming(state, ship, dt);
                return;
            }
            if (ship.Status != ShipStatus.Moving || ship.Route == null)
            {
                TryStartRoam(world, state, ship, dt);
                return;
            }
            var route = ship.Route;
            ship.ArrivedPortId = "";
            bool justLaunched = ship.DistanceAlongRoute <= 0.0001f;
            int prevSeg;
            RouteProgress.SegmentAt(route, ship.DistanceAlongRoute, out prevSeg);
            ship.DistanceAlongRoute += state.Config.SpeedUnitsPerSecond(ship.Speed) * dt;
            int newSeg;
            RouteProgress.SegmentAt(route, Mathf.Min(ship.DistanceAlongRoute, route.length - 0.0001f), out newSeg);
            for (int i = prevSeg + 1; i <= newSeg && i < route.portPath.Count - 1; i++)
            {
                float nodeDist = RouteProgress.CumulativeDistance(route, i);
                if (ship.DistanceAlongRoute >= nodeDist - 0.0001f && nodeDist < route.length - 0.0001f)
                    ship.ArrivedPortId = route.portPath[i];
            }
            if (ship.DistanceAlongRoute >= route.length - 0.0001f)
            {
                FinalizeArrival(state, ship);
                return;
            }
            if (justLaunched || newSeg > prevSeg)
                RollIncident(world, state, ship, EdgeHazard(route, newSeg), justArrived: !justLaunched);
        }
        static void AdvanceRoaming(GameState state, ShipState ship, float dt)
        {
            var route = ship.Route;
            if (route == null) { ship.Status = ShipStatus.Idle; return; }
            ship.DistanceAlongRoute += state.Config.SpeedUnitsPerSecond(ship.Speed)
                * Mathf.Max(0f, state.Config.roamSpeedFactor) * dt;
            if (ship.DistanceAlongRoute < route.length - 0.0001f) return;
            ship.PortId = route.ToPort;
            ship.Status = ShipStatus.Idle;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
            ship.Route = null;
            if (ship.Roam != null) ship.RoamTimerSeconds = ship.Roam.dwellSeconds;
        }
        static void TryStartRoam(SimWorld world, GameState state, ShipState ship, float dt)
        {
            if (ship.Roam == null || !state.Config.roamEnabled) return;
            ship.RoamTimerSeconds -= dt;
            var neighbors = RoamPlanner.Neighbors(world.Routes, ship.PortId);
            if (!RoamPlanner.ShouldLeave(state, ship, neighbors)) return;
            var next = RoamPlanner.ChooseNext(state, ship, neighbors, world.HQPortId);
            ship.LastPortId = ship.PortId;
            ship.Route = next.Route;
            ship.Status = ShipStatus.Roaming;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
        }
        static HazardType EdgeHazard(RouteOption route, int edgeIndex)
        {
            if (route.hazards == null || route.hazards.Count == 0) return HazardType.Any;
            return route.hazards[Mathf.Min(edgeIndex, route.hazards.Count - 1)];
        }
        static void RollIncident(SimWorld world, GameState state, ShipState ship, HazardType edgeHazard, bool justArrived)
        {
            if (state.PendingIncidents.Count >= state.Config.maxPendingIncidents) return;
            OrderState carried = string.IsNullOrEmpty(ship.OrderId)
                ? null
                : state.Orders.Find(o => o.Id == ship.OrderId);
            var ctx = new IncidentContext
            {
                Ship = ship,
                CarriedOrder = carried,
                JustArrivedAtPort = justArrived,
                EdgeHazards = new List<HazardType> { edgeHazard },
            };
            var pick = IncidentScheduler.TryTrigger(world, state, ctx);
            if (pick != null) state.PendingIncidents.Add(pick);
        }
        static void FinalizeArrival(GameState state, ShipState ship)
        {
            ship.LastPortId = ship.Route.FromPort; 
            ship.PortId = ship.Route.ToPort;
            ship.Status = ShipStatus.Idle;
            ship.DistanceAlongRoute = 0f;
            ship.ArrivedPortId = "";
            if (ship.Roam != null) ship.RoamTimerSeconds = ship.Roam.dwellSeconds;
            if (!string.IsNullOrEmpty(ship.OrderId))
            {
                var order = state.Orders.Find(o => o.Id == ship.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.Delivered;
                    order.AssignedShipId = ship.Id;
                    state.Revenue += ScoreCalculator.ScoreDelivery(order, state.Config);
                    state.DeliveredCount++;
                    if (state.DeliveredCount >= state.Config.winDeliveries)
                        EndingEvaluator.FinishRun(state);
                }
                ship.OrderId = "";
            }
            ship.Route = null;
        }
        static void MarkLate(GameState state)
        {
            foreach (var o in state.Orders)
            {
                bool active = o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Assigned;
                if (!active || o.IsLate) continue;
                if (state.ElapsedSeconds <= o.DeadlineAtSeconds) continue;
                o.IsLate = true;
                state.LateCount++;
                if (state.LateCount >= state.Config.maxLateDeliveries)
                {
                    EndingEvaluator.FinishRun(state);
                    return;
                }
            }
        }
        static void ExpireInbox(GameState state)
        {
            state.Orders.RemoveAll(o =>
                o.Status == OrderStatus.InInbox && state.ElapsedSeconds > o.DeadlineAtSeconds);
        }
    }
}

