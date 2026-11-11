namespace AyeAye
{
    // Nơi duy nhất quyết định thắng/thua. Gọi SAU khi cộng quãng đường của ngày hiện tại.
    public static class DeliveryEvaluator
    {
        public const float ArrivalEpsilon = 0.0001f;

        public static void Evaluate(GameState state)
        {
            if (state == null || state.IsFinished) return;

            // Đã đến nơi (kể cả đúng ngày cuối) → đúng hạn.
            if (state.Progress >= state.TotalDistance - ArrivalEpsilon)
            {
                state.ReachedEarly = state.CurrentDay < state.DeadlineDay;
                switch (state.Cargo)
                {
                    case CargoCondition.Scratched: state.FinalEnding = EndingId.MinorDamage; break;
                    case CargoCondition.Ruined: state.FinalEnding = EndingId.DisasterDelivery; break;
                    default: state.FinalEnding = EndingId.PerfectDelivery; break;
                }
                state.IsFinished = true;
                return;
            }

            // Hết ngày cuối mà chưa đến nơi → trễ.
            if (state.CurrentDay >= state.DeadlineDay)
            {
                state.ReachedEarly = false;
                state.FinalEnding = state.Cargo == CargoCondition.Intact
                    ? EndingId.LateButIntact
                    : EndingId.LateDelivery;
                state.IsFinished = true;
                return;
            }

            state.CurrentDay++;
        }
    }
}
