namespace AyeAye
{
    // Kết cục theo luật MỚI (bỏ ngày, xem ASSUMPTIONS.md G1):
    // thắng với 0 lần trễ → E1, 1 → E2, 2 → E3; thua → E4; thua quá sớm (giao ít hơn earlyLoseDeliveries) → E5.
    // Reputation đã bỏ hoàn toàn (chỉnh 4) — kết thúc chỉ theo số đơn thắng/số đơn trễ.
    public static class EndingEvaluator
    {
        public static EndingId FromOutcome(GameState state)
        {
            // Thắng phải đủ đơn: kết thúc cưỡng bức (kẹt/hết giờ) khi chưa đủ winDeliveries là THUA, không phải E1–E3.
            if (state.DeliveredCount >= state.Config.winDeliveries &&
                state.LateCount < state.Config.maxLateDeliveries)
            {
                switch (state.LateCount)
                {
                    case 0: return EndingId.E1_Empire;
                    case 1: return EndingId.E2_Renowned;
                    default: return EndingId.E3_Survived;
                }
            }
            if (state.DeliveredCount < state.Config.earlyLoseDeliveries) return EndingId.E5_Bankrupt;
            return EndingId.E4_Debt;
        }

        public static void FinishRun(GameState state)
        {
            if (state.IsFinished) return;
            state.FinalEnding = FromOutcome(state);
            state.IsFinished = true;
        }
    }
}
