using System.Collections.Generic;

namespace AyeAye
{
    // Vòng ngày do UI điều khiển từng pha (FR-0102): MAKE IT SO → Simulation → Summary → ngày kế.
    // Khác BalanceProbe (tự chơi thay người): Planning là của người chơi; sự cố tự xử theo phương án đầu
    // (shortcut: Bước 5 thay bằng tab SỰ CỐ + nút TIẾP TỤC).
    // Đơn ngày kế bốc SAU khi ++CurrentDay — nạp trước sẽ trúng nhánh Ngày 1 và đè đơn đã nhận bằng bản copy Inbox.
    public static class DayRunner
    {
        // FR-0103/0304: chốt lệnh bất đảo + hết hạn inbox → Simulation.
        public static void CommitPlanning(GameState state)
        {
            SimulationStep.CommitStaged(state);
            state.Phase = GamePhase.Simulation;
        }

        // FR-0501/0502: tàu chạy; sự cố chọn phương án ngẫu nhiên (qua Rng, tái lập được) → Summary.
        public static void Simulate(
            GameState state,
            IReadOnlyList<IncidentDef> incidents, IncidentDef fixedFirstDay, IncidentDef fixedFinale,
            IReadOnlyList<CrewDefinition> crews)
        {
            var contexts = SimulationStep.MovingContexts(state);
            var pendings = IncidentScheduler.Schedule(incidents, fixedFirstDay, fixedFinale, state, contexts);
            foreach (var pending in pendings)
            {
                var option = pending.Incident.options[state.Rng.Next(pending.Incident.options.Count)];
                var resolved = IncidentResolver.Resolve(pending.Incident, option, state, pending.Context.Ship, crews);
                IncidentResolver.Apply(resolved, state, pending.Context.Ship);
                SimulationStep.ApplyProgressDelta(state, pending.Context.Ship, resolved.ProgressDelta);
            }
            SimulationStep.MoveAll(state);
            state.Phase = GamePhase.Summary;
        }

        // FR-0602/0105: chấm điểm đơn giao hôm nay; phá sản (Uy tín ≤ 0) hoặc hết số ngày → kết thúc.
        public static void CompleteSummary(GameState state, OrderPool pool)
        {
            foreach (var order in state.Orders.FindAll(o => o.Status == OrderStatus.Delivered && o.DeliveredDay == state.CurrentDay))
            {
                var (rep, rev) = ScoreCalculator.ScoreDelivery(order, state.Config);
                state.Reputation += rep;
                state.Revenue += rev;
            }

            if (state.Reputation <= 0 || state.CurrentDay >= state.Config.gameLengthDays)
            {
                EndingEvaluator.FinishRun(state);
                return;
            }

            state.CurrentDay++;
            OrderScheduler.ScheduleNewOrders(pool, state);
            state.Phase = GamePhase.Planning;
        }
    }
}
