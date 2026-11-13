using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AyeAye
{
    /// <summary>
    /// Dò cân bằng (G1.9/G3): mô phỏng ván CHẠY LIÊN TỤC bằng Advance theo simTickSeconds,
    /// chính sách NGẪU NHIÊN (nhận đơn theo xúc xắc, tuyến/tuỳ chọn ngẫu nhiên) hoặc ĐỌC MANH MỐI
    /// (nhận đơn về kịp hạn, tuyến ngắn nhất, ưu tiên Smart). Mọi random qua GameState.Rng.
    /// Trả về tỉ lệ thắng (E1/E2/E3).
    /// </summary>
    public static class BalanceProbe
    {
        // Chặn an toàn: ván không tự kết thúc sau thời gian này coi như kẹt (NFR-06).
        const float MaxRunSeconds = 1800f;

        public static float SimulateRuns(
            GameConfig config, OrderPool pool, RouteTable routes,
            IReadOnlyList<IncidentDef> incidents, IncidentDef fixedFirstDay, IncidentDef fixedFinale,
            IReadOnlyList<CrewDefinition> crews, IReadOnlyList<ShipDef> shipDefs,
            int runs, int seedBase, bool cluePolicy = false)
        {
            return SimulateRunsStats(config, pool, routes, incidents, fixedFirstDay, fixedFinale,
                crews, shipDefs, runs, seedBase, cluePolicy).winRate;
        }

        // Đo cả thời lượng ván trung bình (giây mô phỏng) — menu Tools/AyeAye/Balance và G3 dùng.
        public static (float winRate, double avgSeconds) SimulateRunsMeasured(
            GameConfig config, OrderPool pool, RouteTable routes,
            IReadOnlyList<IncidentDef> incidents, IncidentDef fixedFirstDay, IncidentDef fixedFinale,
            IReadOnlyList<CrewDefinition> crews, IReadOnlyList<ShipDef> shipDefs,
            int runs, int seedBase, bool cluePolicy = false)
        {
            var s = SimulateRunsStats(config, pool, routes, incidents, fixedFirstDay, fixedFinale,
                crews, shipDefs, runs, seedBase, cluePolicy);
            return (s.winRate, s.avgSeconds);
        }

        /// <summary>Số liệu đầy đủ cho báo cáo cân bằng: phân bổ kết thúc, percentile thời lượng, trễ TB, tỉ lệ kẹt.</summary>
        public struct RunStats
        {
            public float winRate;
            public double avgSeconds, p10Seconds, p50Seconds, p90Seconds;
            public double avgLateCount;
            public int stuckRuns;
            public int e1, e2, e3, e4, e5;
        }

        public static RunStats SimulateRunsStats(
            GameConfig config, OrderPool pool, RouteTable routes,
            IReadOnlyList<IncidentDef> incidents, IncidentDef fixedFirstDay, IncidentDef fixedFinale,
            IReadOnlyList<CrewDefinition> crews, IReadOnlyList<ShipDef> shipDefs,
            int runs, int seedBase, bool cluePolicy = false)
        {
            var stats = new RunStats();
            if (runs <= 0) return stats;
            var world = new SimWorld
            {
                Pool = pool, Routes = routes, Incidents = incidents,
                FixedFirstDay = fixedFirstDay, FixedFinale = fixedFinale,
            };
            var durations = new List<double>(runs);
            var lateCounts = new List<int>(runs);
            for (int i = 0; i < runs; i++)
            {
                var state = GameStateBuilder.CreateRun(config, shipDefs, seedBase + i);
                OrderScheduler.SpawnInitial(pool, state);
                while (!state.IsFinished && state.ElapsedSeconds < MaxRunSeconds)
                    Step(world, state, crews, cluePolicy);
                bool stuck = !state.IsFinished;
                if (stuck) EndingEvaluator.FinishRun(state); // kẹt → coi như thua, vẫn kết thúc
                durations.Add(state.ElapsedSeconds);
                lateCounts.Add(state.LateCount);
                if (stuck) stats.stuckRuns++;
                switch (state.FinalEnding)
                {
                    case EndingId.E1_Empire: stats.e1++; break;
                    case EndingId.E2_Renowned: stats.e2++; break;
                    case EndingId.E3_Survived: stats.e3++; break;
                    case EndingId.E4_Debt: stats.e4++; break;
                    case EndingId.E5_Bankrupt: stats.e5++; break;
                }
            }
            durations.Sort();
            double Sum(List<double> xs) { double t = 0; foreach (var x in xs) t += x; return t; }
            double Pct(float f) => durations[Mathf.Clamp((int)(durations.Count * f), 0, durations.Count - 1)];
            stats.winRate = (float)(stats.e1 + stats.e2 + stats.e3) / runs;
            stats.avgSeconds = Sum(durations) / runs;
            stats.p10Seconds = Pct(0.1f);
            stats.p50Seconds = Pct(0.5f);
            stats.p90Seconds = Pct(0.9f);
            stats.avgLateCount = (double)lateCounts.Sum() / runs;
            return stats;
        }

        /// <summary>Một tick mô phỏng: xử lý sự cố chờ trước (đồng hồ dừng khi chờ), rồi chính sách + Advance.</summary>
        public static void Step(SimWorld world, GameState state, IReadOnlyList<CrewDefinition> crews, bool cluePolicy)
        {
            if (state.PendingIncidents.Count > 0)
            {
                ResolvePending(state, crews, cluePolicy);
                return;
            }

            Policy(world, state, cluePolicy);
            Simulation.Advance(world, state, state.Config.simTickSeconds);
        }

        static void ResolvePending(GameState state, IReadOnlyList<CrewDefinition> crews, bool cluePolicy)
        {
            var pending = state.PendingIncidents[0];
            var option = cluePolicy
                ? CluePolicy.PickByClue(pending.Incident, state, pending.Context.Ship, crews)
                : pending.Incident.options[state.Rng.Next(pending.Incident.options.Count)];
            var resolved = IncidentResolver.Resolve(pending.Incident, option, state, pending.Context.Ship, crews);
            IncidentResolver.Apply(resolved, state, pending.Context.Ship);
            Simulation.ApplyProgressDelta(state, pending.Context.Ship, resolved.ProgressDelta);
            state.PendingIncidents.RemoveAt(0);
        }

        // Chính sách người chơi thay: nhận + gán ngay mỗi tick (MAKE IT SO áp ngay — không staged).
        static void Policy(SimWorld world, GameState state, bool cluePolicy)
        {
            // 1. Chấp nhận đơn trong hộp (giới hạn đơn đang chạy).
            //    Ngẫu nhiên: theo xúc xắc. Đọc manh mối: nhận khi có tàu rảnh đi đủ tới cảng lấy hàng rồi về kịp hạn.
            foreach (var order in state.Orders.FindAll(o => o.Status == OrderStatus.InInbox))
            {
                bool accept;
                if (cluePolicy)
                {
                    accept = false;
                    float remaining = order.DeadlineAtSeconds - state.ElapsedSeconds;
                    float haul = FastestSeconds(world.Routes, order.FromPortId, order.ToPortId, state.Config);
                    foreach (var ship in state.Ships)
                    {
                        if (ship.Status != ShipStatus.Idle || !string.IsNullOrEmpty(ship.OrderId)) continue;
                        float toPickup = FastestSeconds(world.Routes, ship.PortId, order.FromPortId, state.Config);
                        if (toPickup + haul <= remaining) { accept = true; break; }
                    }
                }
                else
                {
                    accept = state.Rng.Next(2) == 1;
                }
                if (accept && CommandValidator.CanAccept(state, order.Id))
                    order.Status = OrderStatus.Accepted;
            }

            // 2. Gán: với mỗi đơn đã nhận chưa gán, gom ứng viên (tàu đứng ở bến đi × tuyến hợp lệ) → áp ngay.
            foreach (var order in state.Orders.FindAll(o => o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId)))
            {
                var options = world.Routes.GetOptions(order.FromPortId, order.ToPortId);
                if (options == null) continue;
                var candidates = new List<(ShipState ship, RouteOption route)>();
                foreach (var ship in state.Ships)
                {
                    if (ship.Status != ShipStatus.Idle) continue;
                    if (!string.IsNullOrEmpty(ship.OrderId)) continue;
                    for (int i = 0; i < options.Count; i++)
                        if (CommandValidator.ValidateAssign(state, world.Routes, ship.Id, order.Id, i) == null)
                            candidates.Add((ship, options[i]));
                }
                if (candidates.Count == 0) continue;
                var pick = cluePolicy ? BestCandidate(candidates) : candidates[state.Rng.Next(candidates.Count)];
                FleetActions.Assign(state, world.Routes, pick.ship.Id, order.Id, IndexOf(options, pick.route));
            }

            // 3. Đọc manh mối: tàu rảnh rỗi không có việc ở cảng hiện tại → chạy về cảng lấy hàng của
            //    đơn đã nhận chưa có tàu (cổ nhất). Người chơi thật cũng làm đúng thế này.
            if (!cluePolicy) return;
            var demand = state.Orders.Find(o => o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId));
            if (demand == null) return;
            foreach (var ship in state.Ships)
            {
                if (ship.Status != ShipStatus.Idle || !string.IsNullOrEmpty(ship.OrderId)) continue;
                if (ship.PortId == demand.FromPortId) continue;
                var options = world.Routes.GetOptions(ship.PortId, demand.FromPortId);
                if (options == null || options.Count == 0) continue;
                int best = 0;
                for (int i = 1; i < options.Count; i++)
                    if (options[i].length < options[best].length) best = i;
                FleetActions.Reposition(state, world.Routes, ship.Id, demand.FromPortId, best);
            }
        }

        static float FastestSeconds(RouteTable routes, string from, string to, GameConfig config)
        {
            var options = routes.GetOptions(from, to);
            float bestLen = float.MaxValue;
            if (options != null)
                foreach (var opt in options)
                    if (opt.length < bestLen) bestLen = opt.length;
            return bestLen / Mathf.Max(0.0001f, config.SpeedUnitsPerSecond(SpeedClass.Fast));
        }

        static int IndexOf(List<RouteOption> options, RouteOption route)
        {
            for (int i = 0; i < options.Count; i++)
                if (options[i] == route) return i;
            return 0;
        }

        // Đọc manh mối: ứng viên (tàu, tuyến) có độ dài tuyến ngắn nhất — hòa thì lấy đầu tiên (deterministic).
        static (ShipState ship, RouteOption route) BestCandidate(List<(ShipState ship, RouteOption route)> candidates)
        {
            var best = candidates[0];
            foreach (var c in candidates)
                if (c.route.length < best.route.length) best = c;
            return best;
        }
    }
}
