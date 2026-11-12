using System.Collections.Generic;
using AyeAye;

namespace AyeAye
{
    /// <summary>
    /// Dò cân bằng: mô phỏng nhiều ván 8 ngày với chính sách NGẪU NHIÊN
    /// (chấp nhận đơn theo xúc xắc, gán tuyến ngẫu nhiên trong số hợp lệ, chọn tuỳ chọn sự cố ngẫu nhiên).
    /// All randomness qua GameState.Rng → cùng seed cho cùng tỉ lệ thắng.
    /// shortcut: chính sách ngẫu nhiên thô; ngưỡng NFR-07 và chính sách "đọc manh mối" kiểm ở Bước 6.
    /// </summary>
    public static class BalanceProbe
    {
        public static float SimulateRuns(
            GameConfig config, OrderPool pool, RouteTable routes,
            IReadOnlyList<IncidentDef> incidents, IncidentDef fixedFirstDay, IncidentDef fixedFinale,
            IReadOnlyList<CrewDefinition> crews, IReadOnlyList<ShipDef> shipDefs,
            int runs, int seedBase)
        {
            if (runs <= 0) return 0f;
            int wins = 0;
            for (int i = 0; i < runs; i++)
            {
                var state = GameStateBuilder.CreateRun(config, shipDefs, seedBase + i);
                while (!state.IsFinished && state.CurrentDay <= 2 * config.gameLengthDays)
                    SimulateDay(config, pool, routes, incidents, fixedFirstDay, fixedFinale, crews, state);
                if (!state.IsFinished)
                    EndingEvaluator.FinishRun(state);
                if (state.FinalEnding == EndingId.E1_Empire || state.FinalEnding == EndingId.E2_Renowned)
                    wins++;
            }
            return (float)wins / runs;
        }

        /// <summary>Một ngày chơi với chính sách ngẫu nhiên.</summary>
        public static void SimulateDay(
            GameConfig config, OrderPool pool, RouteTable routes,
            IReadOnlyList<IncidentDef> incidents, IncidentDef fixedFirstDay, IncidentDef fixedFinale,
            IReadOnlyList<CrewDefinition> crews, GameState state)
        {
            state.Phase = GamePhase.Planning;

            // 1. Chấp nhận đơn trong hộp theo xúc xắc (giới hạn đơn đang chạy).
            foreach (var order in state.Orders.FindAll(o => o.Status == OrderStatus.InInbox))
            {
                if (state.Rng.Next(2) == 1 && CommandValidator.CanAccept(state, order.Id))
                    order.Status = OrderStatus.Accepted;
            }

            // 2. Gán: với mỗi đơn đã nhận chưa gán, gom ứng viên (tàu đứng yên ở bến đi × tuyến hợp lệ) rồi chọn ngẫu nhiên.
            foreach (var order in state.Orders.FindAll(o => o.Status == OrderStatus.Accepted && string.IsNullOrEmpty(o.AssignedShipId)))
            {
                var options = routes.GetOptions(order.FromPortId, order.ToPortId);
                if (options == null) continue;
                var candidates = new List<(ShipState ship, RouteOption route)>();
                foreach (var ship in state.Ships)
                {
                    if (ship.Status != ShipStatus.Idle) continue;
                    if (!string.IsNullOrEmpty(ship.OrderId)) continue;
                    if (HasStaged(state, ship.Id)) continue;
                    for (int i = 0; i < options.Count; i++)
                        if (CommandValidator.ValidateAssign(state, routes, ship.Id, order.Id, i) == null)
                            candidates.Add((ship, options[i]));
                }
                if (candidates.Count == 0) continue;
                var pick = candidates[state.Rng.Next(candidates.Count)];
                state.StagedCommands.Add(new StagedCommand
                {
                    Kind = CommandKind.Assign,
                    ShipId = pick.ship.Id,
                    OrderId = order.Id,
                    Route = pick.route,
                });
            }

            // 3. MAKE IT SO: chốt lệnh + đơn hộp thư chưa quyết → hủy (FR-0304).
            SimulationStep.CommitStaged(state);
            state.Phase = GamePhase.Simulation;

            // 4. Sự cố cho các tàu đang chạy (chọn tuỳ chọn ngẫu nhiên).
            var contexts = SimulationStep.MovingContexts(state);
            var pendings = IncidentScheduler.Schedule(incidents, fixedFirstDay, fixedFinale, state, contexts);
            foreach (var pending in pendings)
            {
                var option = pending.Incident.options[state.Rng.Next(pending.Incident.options.Count)];
                var resolved = IncidentResolver.Resolve(pending.Incident, option, state, pending.Context.Ship, crews);
                IncidentResolver.Apply(resolved, state, pending.Context.Ship);
                SimulationStep.ApplyProgressDelta(state, pending.Context.Ship, resolved.ProgressDelta);
            }

            // 5. Di chuyển hết lượt rồi tính điểm tổng kết ngày.
            SimulationStep.MoveAll(state);
            state.Phase = GamePhase.Summary;
            foreach (var order in state.Orders.FindAll(o => o.Status == OrderStatus.Delivered && o.DeliveredDay == state.CurrentDay))
            {
                var (rep, rev) = ScoreCalculator.ScoreDelivery(order, config);
                state.Reputation += rep;
                state.Revenue += rev;
            }

            // 6. Kết thúc ngày: phá sản ≤0 ngay lập tức; hết số ngày → kết; còn lại sang ngày kế.
            if (state.Reputation <= 0 || state.CurrentDay >= config.gameLengthDays)
            {
                EndingEvaluator.FinishRun(state);
                return;
            }
            OrderScheduler.ScheduleNewOrders(pool, state);
            state.CurrentDay++;
            state.Phase = GamePhase.Planning;
        }

        private static bool HasStaged(GameState state, string shipId)
        {
            foreach (var cmd in state.StagedCommands)
                if (cmd.ShipId == shipId)
                    return true;
            return false;
        }
    }
}
