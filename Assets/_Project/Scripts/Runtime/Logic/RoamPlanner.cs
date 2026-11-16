using System.Collections.Generic;
using UnityEngine;
namespace AyeAye
{

    public static class RoamPlanner
    {

        public struct Neighbor
        {

            public string PortId;

            public RouteOption Route; 
        }

        public static List<Neighbor> Neighbors(RouteTable routes, string portId)
        {
            var list = new List<Neighbor>();
            if (routes == null || routes.pairs == null || string.IsNullOrEmpty(portId)) return list;
            foreach (var pair in routes.pairs)
            {
                if (pair == null || pair.options == null) continue;
                if (pair.fromPortId != portId) continue;
                foreach (var opt in pair.options)
                {
                    if (opt == null || opt.portPath == null || opt.portPath.Count != 2) continue;
                    list.Add(new Neighbor { PortId = opt.portPath[1], Route = opt });
                }
            }
            return list;
        }

        public static int DemandAt(GameState state, string portId)
        {
            int count = 0;
            foreach (var o in state.Orders)
            {
                if (o.FromPortId != portId) continue;
                if (o.Status == OrderStatus.InInbox) count++;
                else if (o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId)) count++;
            }
            return count;
        }

        public static bool IsExcess(GameState state, ShipState ship)
        {
            int idleHere = 0;
            foreach (var s in state.Ships)
                if (s.Status == ShipStatus.Idle && s.PortId == ship.PortId) idleHere++;
            return idleHere > DemandAt(state, ship.PortId);
        }

        public static Neighbor ChooseNext(GameState state, ShipState ship, List<Neighbor> neighbors, string hqPortId)
        {
            var p = ship.Roam;
            int best = 0;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < neighbors.Count; i++)
            {
                var nb = neighbors[i];
                int demand = p.demandWeight != 0f ? DemandAt(state, nb.PortId) : 0;
                float score = p.demandWeight * demand
                    + p.farBias * nb.Route.length
                    + (!string.IsNullOrEmpty(hqPortId) && nb.PortId == hqPortId ? p.hubAffinity : 0f)
                    - (nb.PortId == ship.LastPortId ? p.backtrackPenalty : 0f)
                    + p.wanderWeight * (float)state.RoamRng.NextDouble();
                if (score > bestScore) { bestScore = score; best = i; } 
            }
            return neighbors[best];
        }

        public static bool ShouldLeave(GameState state, ShipState ship, List<Neighbor> neighbors)
        {
            var config = state.Config;
            if (!config.roamEnabled || ship.Roam == null) return false;
            if (state.ElapsedSeconds < config.roamStartSeconds) return false;
            if (ship.RoamTimerSeconds > 0f) return false;
            if (neighbors.Count == 0) return false;
            return IsExcess(state, ship);
        }
    }
}

