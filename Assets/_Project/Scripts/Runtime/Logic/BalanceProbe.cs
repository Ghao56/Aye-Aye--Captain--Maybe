namespace AyeAye
{
    // Đo tỷ lệ thắng khi chơi ngẫu nhiên hoàn toàn (NFR-07: "chọn ngẫu nhiên không thể thắng").
    // Thuần C#: nạp config/pool và một CrewDefinition dự phòng từ caller (Editor test, Validate Demo).
    // Win = kết thúc đúng hạn: PerfectDelivery / MinorDamage / DisasterDelivery.
    public static class BalanceProbe
    {
        public const float WinRateLimit = 0.15f;

        public static float SimulateWinRate(GameConfig config, EventPool pool, int runs, CrewDefinition fallbackCrew)
        {
            int wins = 0;
            CrewId[] crews = { CrewId.Bob, CrewId.Mira, CrewId.Chen, CrewId.Dex };
            for (int seed = 0; seed < runs; seed++)
            {
                GameState state = new GameState(config, seed);
                System.Random rng = new System.Random(seed + 1);
                while (!state.IsFinished)
                {
                    DayDefinition ev = state.CurrentDay == 1 ? pool.openingDay
                        : EventScheduler.Pick(pool.events, state.UsedEvents, state, state.Rng);
                    state.UsedEvents.Add(ev);
                    DecisionOption option = ev.decision.options[rng.Next(ev.decision.options.Count)];
                    ResolvedDecision resolved = DecisionResolver.Resolve(
                        option, crews[rng.Next(crews.Length)], state, fallbackCrew);
                    CargoTracker.Apply(state, resolved);
                    state.Progress += TravelProgress.GetDistance(resolved.Outcome, config);
                    DeliveryEvaluator.Evaluate(state);
                }
                if (state.FinalEnding == EndingId.PerfectDelivery
                    || state.FinalEnding == EndingId.MinorDamage
                    || state.FinalEnding == EndingId.DisasterDelivery)
                    wins++;
            }
            return wins / (float)runs;
        }
    }
}
