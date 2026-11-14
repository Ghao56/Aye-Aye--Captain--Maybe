using System.Collections.Generic;
using UnityEngine;

namespace AyeAye
{
    // G5 — planner roaming thuần C#: chọn cảng kề TRỰC TIẾP có điểm cao nhất.
    // Điểm = nhu cầu đơn + tính cách (farBias/hubAffinity/wander) − backtrack. Hòa → cảng đứng trước
    // theo thứ tự ổn định của bảng tuyến (xác định theo seed qua RoamRng cho phần nhiễu).
    // CẤM dùng state.Rng ở đây (test RoamKhongDungRngChung chốt bằng thực nghiệm).
    public static class RoamPlanner
    {
        public struct Neighbor
        {
            public string PortId;
            public RouteOption Route; // RouteOption 1 cạnh (portPath.Count == 2)
        }

        // Các cảng kề trực tiếp từ portId, thứ tự ổn định theo RouteTable.pairs.
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

        // Nhu cầu tại một cảng: đơn hộp thư + đơn đã nhận CHƯA gán tàu xuất phát từ cảng đó.
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

        // Tàu là DƯ THỪA tại cảng: số tàu Idle tại cảng > nhu cầu của cảng đó.
        // Mục đích: tàu không bỏ đi khi đơn đang chờ ngay tại cảng.
        public static bool IsExcess(GameState state, ShipState ship)
        {
            int idleHere = 0;
            foreach (var s in state.Ships)
                if (s.Status == ShipStatus.Idle && s.PortId == ship.PortId) idleHere++;
            return idleHere > DemandAt(state, ship.PortId);
        }

        // Chọn cảng kề theo điểm (hồ sơ đọc từ ship.Roam, không hard-code).
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
                if (score > bestScore) { bestScore = score; best = i; } // hòa → giữ cảng đứng trước
            }
            return neighbors[best];
        }

        // Điều kiện rời cảng (kiểm mỗi tick khi Idle): hết ân hạn + hết nghỉ + dư thừa.
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
